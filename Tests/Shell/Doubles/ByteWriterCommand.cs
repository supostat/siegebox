using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Process.Tests;

namespace Siegebox.Shell.Tests
{
    /// <summary>Writes its whole payload in the first Step — the first-output-loss probe.</summary>
    internal sealed class ByteWriterCommand : ICommand
    {
        private readonly byte[] payload;

        public ByteWriterCommand(string name, byte[] payload)
        {
            Name = name;
            this.payload = payload;
        }

        public string Name { get; }

        public WriterProcess LastProcess { get; private set; }

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            LastProcess = new WriterProcess(context, payload, payload.Length);
            return LastProcess;
        }
    }
}
