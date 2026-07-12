using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Runs a builtin against its target session (live for a sole foreground command,
    /// a clone in subshell positions). A WaitFor result sleeps on the awaited pid; a
    /// ReadLine result writes a prompt, reads one line from stdin (byte-at-a-time so it
    /// never steals the shell's next input), and re-invokes the builtin with that line.
    /// </summary>
    internal sealed class BuiltinProcess : IProcess
    {
        private readonly IBuiltin builtin;
        private readonly ShellSession target;
        private readonly IReadOnlyList<string> arguments;
        private readonly PendingWriteQueue pendingWrites = new PendingWriteQueue();
        private readonly List<byte> lineBytes = new List<byte>();
        private readonly byte[] oneByte = new byte[1];
        private bool executed;
        private string? deliveredLine;
        private string? promptToWrite;
        private bool promptEnqueued;
        private bool awaitingLine;

        public BuiltinProcess(IBuiltin builtin, ShellSession target, IReadOnlyList<string> arguments, ExecutionContext context)
        {
            this.builtin = builtin;
            this.target = target;
            this.arguments = arguments;
            Context = context;
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public ProcessState Step()
        {
            return RunBuiltinPhase() ?? PromptPhase() ?? ReadLinePhase() ?? DrainPhase();
        }

        private ProcessState? RunBuiltinPhase()
        {
            if (executed || promptToWrite != null || awaitingLine)
            {
                return null;
            }

            var result = ExecuteSafely(deliveredLine);
            deliveredLine = null;
            if (result.WaitForPid > 0)
            {
                WakeCondition = WakeCondition.ProcessExit(result.WaitForPid);
                return ProcessState.Sleeping;
            }

            if (result.ReadLinePrompt != null)
            {
                promptToWrite = result.ReadLinePrompt;
                return null;
            }

            executed = true;
            ExitCode = result.ExitCode;
            pendingWrites.Enqueue(Stdout, result.Output);
            pendingWrites.Enqueue(Stderr, result.Error);
            return null;
        }

        private ProcessState? PromptPhase()
        {
            if (promptToWrite == null)
            {
                return null;
            }

            if (!promptEnqueued)
            {
                pendingWrites.Enqueue(Stdout, promptToWrite);
                promptEnqueued = true;
            }

            if (pendingWrites.Drain() == DrainStatus.WouldBlock)
            {
                WakeCondition = WakeCondition.Writable(pendingWrites.BlockedTarget!);
                return ProcessState.Sleeping;
            }

            promptToWrite = null;
            promptEnqueued = false;
            awaitingLine = true;
            return null;
        }

        private ProcessState? ReadLinePhase()
        {
            if (!awaitingLine)
            {
                return null;
            }

            if (!TryReadLine(out var line))
            {
                WakeCondition = WakeCondition.Readable(Stdin);
                return ProcessState.Sleeping;
            }

            awaitingLine = false;
            deliveredLine = line;
            return ProcessState.Running;
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

        private bool TryReadLine(out string line)
        {
            line = string.Empty;
            while (true)
            {
                var result = Stdin.Read(oneByte, 0, 1);
                if (result.Status == StreamStatus.WouldBlock || (result.Status == StreamStatus.Ok && result.Count == 0))
                {
                    return false;
                }

                if (result.Status == StreamStatus.Eof || oneByte[0] == (byte)'\n')
                {
                    line = DecodeLine();
                    return true;
                }

                lineBytes.Add(oneByte[0]);
            }
        }

        private string DecodeLine()
        {
            var text = Encoding.UTF8.GetString(lineBytes.ToArray()).TrimEnd('\r');
            lineBytes.Clear();
            return text;
        }

        private BuiltinResult ExecuteSafely(string? inputLine)
        {
            try
            {
                return builtin.Execute(target, arguments, inputLine);
            }
            catch (VfsException error)
            {
                return BuiltinResult.Completed(
                    1,
                    "",
                    $"{builtin.Name}: {error.Path}: {VfsErrorText.MessageFor(error.Error)}\n");
            }
        }

        private IByteStream Stdin => Context.FileDescriptors.Get(FileDescriptorTable.Stdin);

        private IByteStream Stdout => Context.FileDescriptors.Get(FileDescriptorTable.Stdout);

        private IByteStream Stderr => Context.FileDescriptors.Get(FileDescriptorTable.Stderr);
    }
}
