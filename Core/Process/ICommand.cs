using System.Collections.Generic;

namespace Siegebox.Process
{
    /// <summary>Process factory: builds a process instance for a spawn request.</summary>
    public interface ICommand
    {
        string Name { get; }

        IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments);
    }
}
