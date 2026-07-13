using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// The interactive engine behind <see cref="PasswdCommand"/>: a cooperative state machine
    /// that prompts, reads each line byte-at-a-time through the shared <see cref="LineReader"/>,
    /// verifies the old password (for a non-root real identity) and rewrites /etc/shadow through
    /// the resolver under the EFFECTIVE identity. Every step is contained — a VfsException
    /// becomes a unix error line, any other failure an "internal error" — so a denied read
    /// leaves shadow untouched, and the write happens only after every check passes.
    /// </summary>
    internal sealed class PasswdProcess : IProcess
    {
        private const string CurrentPasswordPrompt = "Current password: ";
        private const string NewPasswordPrompt = "New password: ";
        private const string RetypePasswordPrompt = "Retype new password: ";

        private static readonly string[] RootPrompts = { NewPasswordPrompt, RetypePasswordPrompt };
        private static readonly string[] UserPrompts = { CurrentPasswordPrompt, NewPasswordPrompt, RetypePasswordPrompt };

        private readonly VirtualFileSystem vfs;
        private readonly AuthenticationService authentication;
        private readonly IReadOnlyList<string> arguments;
        private readonly LineReader lineReader = new LineReader();
        private readonly List<string> answers = new List<string>();
        private PendingWriteQueue pendingWrites = new PendingWriteQueue();
        private string[] prompts = Array.Empty<string>();
        private string targetName = string.Empty;
        private string realUserName = string.Empty;
        private bool realRoot;
        private bool started;
        private bool completed;
        private int promptIndex;
        private bool promptEnqueued;
        private bool promptWritten;

        public PasswdProcess(
            ExecutionContext context,
            VirtualFileSystem vfs,
            AuthenticationService authentication,
            IReadOnlyList<string> arguments)
        {
            Context = context;
            this.vfs = vfs;
            this.authentication = authentication;
            this.arguments = arguments;
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public ProcessState Step()
        {
            try
            {
                return RunPhases();
            }
            catch (VfsException error)
            {
                return FailWith($"passwd: {error.Path}: {VfsErrorText.MessageFor(error.Error)}\n");
            }
            catch (Exception)
            {
                return FailWith("passwd: internal error\n");
            }
        }

        private ProcessState RunPhases()
        {
            if (!started)
            {
                started = true;
                ResolveStartup();
            }

            if (!completed)
            {
                var phase = PromptPhase() ?? ReadPhase() ?? ApplyPhase();
                if (phase.HasValue)
                {
                    return phase.Value;
                }
            }

            return DrainPhase();
        }

        private void ResolveStartup()
        {
            realRoot = Context.RealCredentials.IsRoot;
            realUserName = authentication.TryResolveByUid(Context.RealCredentials.Uid, out var record) ? record.Name : string.Empty;

            if (arguments.Count > 1)
            {
                Complete(1, "", "passwd: too many arguments\n");
                return;
            }

            if (!TryDetermineTarget(out var failure))
            {
                Complete(1, "", failure);
                return;
            }

            if (!realRoot && targetName != realUserName)
            {
                Complete(1, "", $"passwd: you may not change the password for {targetName}\n");
                return;
            }

            prompts = realRoot ? RootPrompts : UserPrompts;
        }

        private bool TryDetermineTarget(out string failure)
        {
            failure = string.Empty;
            if (arguments.Count == 1)
            {
                if (!authentication.TryResolveByName(arguments[0], out _))
                {
                    failure = $"passwd: user '{arguments[0]}' does not exist\n";
                    return false;
                }

                targetName = arguments[0];
                return true;
            }

            if (realUserName.Length == 0)
            {
                failure = "passwd: cannot determine the caller's user name\n";
                return false;
            }

            targetName = realUserName;
            return true;
        }

        private ProcessState? PromptPhase()
        {
            if (promptIndex >= prompts.Length || promptWritten)
            {
                return null;
            }

            if (!promptEnqueued)
            {
                pendingWrites.Enqueue(Stdout, SecretPromptMarker.Sequence + prompts[promptIndex]);
                promptEnqueued = true;
            }

            if (pendingWrites.Drain() == DrainStatus.WouldBlock)
            {
                WakeCondition = WakeCondition.Writable(pendingWrites.BlockedTarget!);
                return ProcessState.Sleeping;
            }

            promptEnqueued = false;
            promptWritten = true;
            return null;
        }

        private ProcessState? ReadPhase()
        {
            if (promptIndex >= prompts.Length || !promptWritten)
            {
                return null;
            }

            if (!lineReader.TryReadLine(Stdin, out var line))
            {
                WakeCondition = WakeCondition.Readable(Stdin);
                return ProcessState.Sleeping;
            }

            answers.Add(line);
            promptIndex++;
            promptWritten = false;
            return ProcessState.Running;
        }

        private ProcessState? ApplyPhase()
        {
            if (promptIndex < prompts.Length)
            {
                return null;
            }

            Apply();
            return null;
        }

        private void Apply()
        {
            var newIndex = realRoot ? 0 : 1;
            var retypeIndex = realRoot ? 1 : 2;

            if (!realRoot && !authentication.Authenticate(realUserName, answers[0]))
            {
                Complete(1, "", "passwd: authentication failure\n");
                return;
            }

            var newPassword = answers[newIndex];
            if (newPassword.Length == 0)
            {
                Complete(1, "", "passwd: password unchanged\n");
                return;
            }

            if (newPassword != answers[retypeIndex])
            {
                Complete(1, "", "passwd: passwords do not match\n");
                return;
            }

            WriteShadow(targetName, newPassword);
            Complete(0, "passwd: password updated successfully\n", "");
        }

        private void WriteShadow(string name, string password)
        {
            var shadow = ShadowTable.Parse(ShadowFile.Read(vfs, Context.Credentials));
            shadow.SetHash(name, PasswordHash.Create(password));
            ShadowFile.Write(vfs, Context.Credentials, shadow.Render());
        }

        private ProcessState DrainPhase()
        {
            if (pendingWrites.Drain() == DrainStatus.WouldBlock)
            {
                WakeCondition = WakeCondition.Writable(pendingWrites.BlockedTarget!);
                return ProcessState.Sleeping;
            }

            return ProcessState.Finished;
        }

        private void Complete(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            pendingWrites.Enqueue(Stdout, output);
            pendingWrites.Enqueue(Stderr, error);
            completed = true;
        }

        private ProcessState FailWith(string message)
        {
            ExitCode = 1;
            completed = true;
            pendingWrites = new PendingWriteQueue();
            pendingWrites.Enqueue(Stderr, message);
            return DrainPhase();
        }

        private IByteStream Stdin => Context.FileDescriptors.Get(FileDescriptorTable.Stdin);

        private IByteStream Stdout => Context.FileDescriptors.Get(FileDescriptorTable.Stdout);

        private IByteStream Stderr => Context.FileDescriptors.Get(FileDescriptorTable.Stderr);
    }
}
