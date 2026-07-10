using System.Text;
using Siegebox.Process;

namespace Siegebox.Shell
{
    /// <summary>
    /// A stage that only emits one message and exits: the uniform error channel for parse
    /// errors (2), command-not-found (127) and failed redirects (1), so sibling stages and
    /// pipe cascades keep working.
    /// </summary>
    internal sealed class MessageProcess : IProcess
    {
        private readonly PendingWrite message;

        public MessageProcess(ExecutionContext context, string message, int descriptor, int exitCode)
        {
            Context = context;
            ExitCode = exitCode;
            this.message = new PendingWrite(
                context.FileDescriptors.Get(descriptor),
                Encoding.UTF8.GetBytes(message));
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; }

        public WakeCondition WakeCondition { get; private set; }

        public ProcessState Step()
        {
            if (message.Advance() == DrainStatus.WouldBlock)
            {
                WakeCondition = WakeCondition.Writable(message.Target);
                return ProcessState.Sleeping;
            }

            return ProcessState.Finished;
        }
    }
}
