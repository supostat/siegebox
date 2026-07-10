using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class Job
    {
        public Job(int number, IReadOnlyList<int> pids, int lastPid, string description)
        {
            if (pids is null)
            {
                throw new ArgumentNullException(nameof(pids));
            }

            if (pids.Count == 0)
            {
                throw new ArgumentException("A job needs at least one pid.", nameof(pids));
            }

            if (description is null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            Number = number;
            Pids = pids;
            LastPid = lastPid;
            Description = description;
        }

        public int Number { get; }

        public IReadOnlyList<int> Pids { get; }

        public int LastPid { get; }

        public string Description { get; }
    }
}
