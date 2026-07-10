using System.Collections.Generic;

namespace Siegebox.Shell
{
    internal sealed class LaunchedPipeline
    {
        public LaunchedPipeline(IReadOnlyList<int> pids, string description)
        {
            Pids = pids;
            Description = description;
        }

        public IReadOnlyList<int> Pids { get; }

        public int LastPid => Pids[Pids.Count - 1];

        public string Description { get; }
    }
}
