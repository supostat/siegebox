using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Process.Tests;

namespace Siegebox.Shell.Tests
{
    /// <summary>Reads stdin to EOF, recording every byte.</summary>
    internal sealed class ByteReaderCommand : ICommand
    {
        public ByteReaderCommand(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public ReaderProcess LastProcess { get; private set; }

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            LastProcess = new ReaderProcess(context, 8);
            return LastProcess;
        }
    }
}
