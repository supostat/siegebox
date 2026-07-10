using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Process.Tests;

namespace Siegebox.Shell.Tests
{
    /// <summary>Records every spawn context, then exits immediately with the configured code.</summary>
    internal sealed class ProbeCommand : ICommand
    {
        private readonly int exitCode;

        public ProbeCommand(string name, int exitCode = 0)
        {
            Name = name;
            this.exitCode = exitCode;
        }

        public string Name { get; }

        public List<ExecutionContext> Contexts { get; } = new List<ExecutionContext>();

        public List<IReadOnlyList<string>> ArgumentLists { get; } = new List<IReadOnlyList<string>>();

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            Contexts.Add(context);
            ArgumentLists.Add(arguments);
            return new ScriptedProcess(context, self =>
            {
                self.ExitCode = exitCode;
                return ProcessState.Finished;
            });
        }
    }
}
