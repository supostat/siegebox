using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Process.Tests;

namespace Siegebox.Shell.Tests
{
    /// <summary>Runs forever; only a kill ends it.</summary>
    internal sealed class SpinCommand : ICommand
    {
        public SpinCommand(string name = "spin")
        {
            Name = name;
        }

        public string Name { get; }

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
            => new ScriptedProcess(context, self => ProcessState.Running);
    }
}
