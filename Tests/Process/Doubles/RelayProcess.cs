using System;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    internal sealed class RelayProcess : IProcess
    {
        private readonly byte[] pending;
        private int pendingOffset;
        private int pendingCount;

        public RelayProcess(ExecutionContext context, int chunkSize)
        {
            Context = context;
            pending = new byte[chunkSize];
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public ProcessState Step()
        {
            if (pendingCount == 0)
            {
                return FillFromStandardInput();
            }

            return FlushToStandardOutput();
        }

        private ProcessState FillFromStandardInput()
        {
            var standardInput = Context.FileDescriptors.Get(FileDescriptorTable.Stdin);
            var result = standardInput.Read(pending, 0, pending.Length);
            switch (result.Status)
            {
                case StreamStatus.Ok:
                    pendingOffset = 0;
                    pendingCount = result.Count;
                    return ProcessState.Running;
                case StreamStatus.WouldBlock:
                    WakeCondition = WakeCondition.Readable(standardInput);
                    return ProcessState.Sleeping;
                case StreamStatus.Eof:
                    ExitCode = 0;
                    return ProcessState.Finished;
                default:
                    throw new InvalidOperationException($"Unexpected stream status {result.Status}.");
            }
        }

        private ProcessState FlushToStandardOutput()
        {
            var standardOutput = Context.FileDescriptors.Get(FileDescriptorTable.Stdout);
            var result = standardOutput.Write(pending, pendingOffset, pendingCount);
            switch (result.Status)
            {
                case StreamStatus.Ok:
                    pendingOffset += result.Count;
                    pendingCount -= result.Count;
                    return ProcessState.Running;
                case StreamStatus.WouldBlock:
                    WakeCondition = WakeCondition.Writable(standardOutput);
                    return ProcessState.Sleeping;
                case StreamStatus.Eof:
                    ExitCode = WriterProcess.BrokenPipeExitCode;
                    return ProcessState.Finished;
                default:
                    throw new InvalidOperationException($"Unexpected stream status {result.Status}.");
            }
        }
    }
}
