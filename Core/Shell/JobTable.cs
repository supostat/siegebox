using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class JobTable
    {
        private readonly List<Job> jobs = new List<Job>();

        public IReadOnlyList<Job> Jobs => jobs;

        public Job Add(IReadOnlyList<int> pids, int lastPid, string description)
        {
            var job = new Job(NextNumber(), pids, lastPid, description);
            jobs.Add(job);
            return job;
        }

        public void Remove(int number)
        {
            for (var index = 0; index < jobs.Count; index++)
            {
                if (jobs[index].Number == number)
                {
                    jobs.RemoveAt(index);
                    return;
                }
            }

            throw new ArgumentException($"Unknown job number {number}.", nameof(number));
        }

        private int NextNumber()
        {
            var highest = 0;
            foreach (var job in jobs)
            {
                highest = Math.Max(highest, job.Number);
            }

            return highest + 1;
        }
    }
}
