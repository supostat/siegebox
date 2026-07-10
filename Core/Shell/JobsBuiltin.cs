using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;

namespace Siegebox.Shell
{
    /// <summary>
    /// Lists jobs as "[n] lastPid Running|Done description"; a Done job is reported once,
    /// then removed with its retained statuses collected.
    /// </summary>
    public sealed class JobsBuiltin : IBuiltin
    {
        private readonly JobTable jobs;
        private readonly JobStatusCollector collector;

        public JobsBuiltin(Scheduler scheduler, JobTable jobs)
        {
            if (scheduler is null)
            {
                throw new ArgumentNullException(nameof(scheduler));
            }

            this.jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
            collector = new JobStatusCollector(scheduler, jobs);
        }

        public string Name => "jobs";

        public BuiltinResult Execute(ShellSession session, IReadOnlyList<string> arguments)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var output = new StringBuilder();
            foreach (var job in new List<Job>(jobs.Jobs))
            {
                var done = collector.IsJobFinished(job);
                output.Append($"[{job.Number}] {job.LastPid} {(done ? "Done" : "Running")} {job.Description}\n");
                if (done)
                {
                    collector.CollectAndRemove(job);
                }
            }

            return BuiltinResult.Completed(0, output.ToString());
        }
    }
}
