using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Base for commands that compute their whole result in one slice and then drain it through
    /// stdout/stderr under backpressure. A VfsException inside <see cref="Run"/> becomes a unix
    /// error line and exit code 1; a closed output ends the drain with the outcome's exit code.
    /// </summary>
    internal abstract class BufferedCommandProcess : IProcess
    {
        private readonly PendingWriteQueue pendingWrites = new PendingWriteQueue();
        private bool started;

        protected BufferedCommandProcess(ExecutionContext context)
        {
            Context = context;
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        protected abstract string CommandName { get; }

        protected abstract CommandOutcome Run();

        public ProcessState Step()
        {
            if (!started)
            {
                started = true;
                var outcome = RunSafely();
                ExitCode = outcome.ExitCode;
                pendingWrites.Enqueue(Context.FileDescriptors.Get(FileDescriptorTable.Stdout), outcome.Output);
                pendingWrites.Enqueue(Context.FileDescriptors.Get(FileDescriptorTable.Stderr), outcome.Error);
            }

            if (pendingWrites.Drain() == DrainStatus.WouldBlock)
            {
                WakeCondition = WakeCondition.Writable(pendingWrites.BlockedTarget!);
                return ProcessState.Sleeping;
            }

            return ProcessState.Finished;
        }

        private CommandOutcome RunSafely()
        {
            try
            {
                return Run();
            }
            catch (VfsException error)
            {
                return CommandOutcome.Fail(1, $"{CommandName}: {error.Path}: {VfsErrorText.MessageFor(error.Error)}\n");
            }
        }
    }
}
