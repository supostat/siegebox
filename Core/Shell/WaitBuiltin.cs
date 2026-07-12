using System;
using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell
{
    /// <summary>
    /// Without arguments waits for every job, collecting finished jobs' retained statuses;
    /// with pid arguments waits for each in turn and exits with the last pid's status
    /// (127 for an unknown pid). Blocking is cooperative: the first unfinished pid yields
    /// a WaitFor result and the builtin is re-invoked after that pid exits.
    /// </summary>
    public sealed class WaitBuiltin : IBuiltin
    {
        private readonly Scheduler scheduler;
        private readonly JobTable jobs;
        private readonly JobStatusCollector collector;

        public WaitBuiltin(Scheduler scheduler, JobTable jobs)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
            collector = new JobStatusCollector(scheduler, jobs);
        }

        public string Name => "wait";

        public BuiltinResult Execute(ShellSession session, IReadOnlyList<string> arguments, string? inputLine)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return arguments.Count == 0 ? WaitForAllJobs() : WaitForPids(arguments);
        }

        private BuiltinResult WaitForAllJobs()
        {
            foreach (var job in new List<Job>(jobs.Jobs))
            {
                var unfinishedPid = collector.FirstUnfinishedPid(job);
                if (unfinishedPid > 0)
                {
                    return BuiltinResult.WaitFor(unfinishedPid);
                }

                collector.CollectAndRemove(job);
            }

            return BuiltinResult.Completed(0);
        }

        private BuiltinResult WaitForPids(IReadOnlyList<string> arguments)
        {
            var pids = new int[arguments.Count];
            for (var index = 0; index < arguments.Count; index++)
            {
                if (!int.TryParse(arguments[index], out pids[index]))
                {
                    return BuiltinResult.Completed(127, "", $"wait: {arguments[index]}: not a pid\n");
                }
            }

            foreach (var pid in pids)
            {
                if (!collector.BelongsToSession(pid))
                {
                    return BuiltinResult.Completed(127, "", $"wait: pid {pid} is not a child of this shell\n");
                }
            }

            foreach (var pid in pids)
            {
                if (!collector.IsFinished(pid))
                {
                    return BuiltinResult.WaitFor(pid);
                }
            }

            var lastExitCode = 127;
            foreach (var pid in pids)
            {
                if (scheduler.TryCollectExitCode(pid, out var exitCode) && pid == pids[pids.Length - 1])
                {
                    lastExitCode = exitCode;
                }
            }

            return BuiltinResult.Completed(lastExitCode);
        }
    }
}
