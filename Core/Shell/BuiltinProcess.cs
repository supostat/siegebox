using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Runs a builtin against its target session (live for a sole foreground command,
    /// a clone in subshell positions). A WaitFor result puts the process to sleep on the
    /// awaited pid and re-invokes the builtin after the wake.
    /// </summary>
    internal sealed class BuiltinProcess : IProcess
    {
        private readonly IBuiltin builtin;
        private readonly ShellSession target;
        private readonly IReadOnlyList<string> arguments;
        private readonly PendingWriteQueue pendingWrites = new PendingWriteQueue();
        private bool executed;

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
            if (!executed)
            {
                var result = ExecuteSafely();
                if (result.WaitForPid > 0)
                {
                    WakeCondition = WakeCondition.ProcessExit(result.WaitForPid);
                    return ProcessState.Sleeping;
                }

                executed = true;
                ExitCode = result.ExitCode;
                pendingWrites.Enqueue(Context.FileDescriptors.Get(FileDescriptorTable.Stdout), result.Output);
                pendingWrites.Enqueue(Context.FileDescriptors.Get(FileDescriptorTable.Stderr), result.Error);
            }

            if (pendingWrites.Drain() == DrainStatus.WouldBlock)
            {
                WakeCondition = WakeCondition.Writable(pendingWrites.BlockedTarget!);
                return ProcessState.Sleeping;
            }

            return ProcessState.Finished;
        }

        private BuiltinResult ExecuteSafely()
        {
            try
            {
                return builtin.Execute(target, arguments);
            }
            catch (VfsException error)
            {
                return BuiltinResult.Completed(
                    1,
                    "",
                    $"{builtin.Name}: {error.Path}: {VfsErrorText.MessageFor(error.Error)}\n");
            }
        }
    }
}
