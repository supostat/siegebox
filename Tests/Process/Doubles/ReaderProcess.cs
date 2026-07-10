using System;
using System.Collections.Generic;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    internal sealed class ReaderProcess : IProcess
    {
        private readonly List<byte> received = new List<byte>();
        private readonly byte[] chunk;

        public ReaderProcess(ExecutionContext context, int chunkSize)
        {
            Context = context;
            chunk = new byte[chunkSize];
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public bool SawEof { get; private set; }

        public byte[] Received => received.ToArray();

        public ProcessState Step()
        {
            var standardInput = Context.FileDescriptors.Get(FileDescriptorTable.Stdin);
            var result = standardInput.Read(chunk, 0, chunk.Length);
            switch (result.Status)
            {
                case StreamStatus.Ok:
                    for (var index = 0; index < result.Count; index++)
                    {
                        received.Add(chunk[index]);
                    }

                    return ProcessState.Running;
                case StreamStatus.WouldBlock:
                    WakeCondition = WakeCondition.Readable(standardInput);
                    return ProcessState.Sleeping;
                case StreamStatus.Eof:
                    SawEof = true;
                    ExitCode = 0;
                    return ProcessState.Finished;
                default:
                    throw new InvalidOperationException($"Unexpected stream status {result.Status}.");
            }
        }
    }
}
