using System;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    internal sealed class WriterProcess : IProcess
    {
        public const int BrokenPipeExitCode = 1;

        private readonly byte[] payload;
        private readonly int chunkSize;
        private int position;

        public WriterProcess(ExecutionContext context, byte[] payload, int chunkSize)
        {
            Context = context;
            this.payload = payload;
            this.chunkSize = chunkSize;
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public int BlockedCount { get; private set; }

        public bool SawBrokenPipe { get; private set; }

        public ProcessState Step()
        {
            if (position == payload.Length)
            {
                ExitCode = 0;
                return ProcessState.Finished;
            }

            var standardOutput = Context.FileDescriptors.Get(FileDescriptorTable.Stdout);
            var count = Math.Min(chunkSize, payload.Length - position);
            var result = standardOutput.Write(payload, position, count);
            switch (result.Status)
            {
                case StreamStatus.Ok:
                    position += result.Count;
                    if (position == payload.Length)
                    {
                        ExitCode = 0;
                        return ProcessState.Finished;
                    }

                    return ProcessState.Running;
                case StreamStatus.WouldBlock:
                    BlockedCount++;
                    WakeCondition = WakeCondition.Writable(standardOutput);
                    return ProcessState.Sleeping;
                case StreamStatus.Eof:
                    SawBrokenPipe = true;
                    ExitCode = BrokenPipeExitCode;
                    return ProcessState.Finished;
                default:
                    throw new InvalidOperationException($"Unexpected stream status {result.Status}.");
            }
        }
    }
}
