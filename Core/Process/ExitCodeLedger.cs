using System.Collections.Generic;

namespace Siegebox.Process
{
    /// <summary>
    /// Explicit zombie semantics: a finished process's exit code is retained here at
    /// termination and lives until exactly one caller collects it.
    /// </summary>
    internal sealed class ExitCodeLedger
    {
        private readonly Dictionary<int, int> retained = new Dictionary<int, int>();

        public void Retain(int pid, int exitCode) => retained[pid] = exitCode;

        public bool TryPeek(int pid, out int exitCode) => retained.TryGetValue(pid, out exitCode);

        public bool TryCollect(int pid, out int exitCode)
        {
            if (!retained.TryGetValue(pid, out exitCode))
            {
                return false;
            }

            retained.Remove(pid);
            return true;
        }
    }
}
