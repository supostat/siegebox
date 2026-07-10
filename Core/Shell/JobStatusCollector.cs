using Siegebox.Process;

namespace Siegebox.Shell
{
    /// <summary>
    /// Shared job bookkeeping for wait/jobs: finished-pid predicate over the exit-code ledger,
    /// and the collect-then-remove step that ends a job's zombie state.
    /// </summary>
    internal sealed class JobStatusCollector
    {
        private readonly Scheduler scheduler;
        private readonly JobTable jobs;

        public JobStatusCollector(Scheduler scheduler, JobTable jobs)
        {
            this.scheduler = scheduler;
            this.jobs = jobs;
        }

        public bool IsFinished(int pid) => scheduler.TryPeekExitCode(pid, out _) || !scheduler.Contains(pid);

        public int FirstUnfinishedPid(Job job)
        {
            foreach (var pid in job.Pids)
            {
                if (!IsFinished(pid))
                {
                    return pid;
                }
            }

            return 0;
        }

        public bool IsJobFinished(Job job) => FirstUnfinishedPid(job) == 0;

        public void CollectAndRemove(Job job)
        {
            foreach (var pid in job.Pids)
            {
                scheduler.TryCollectExitCode(pid, out _);
            }

            jobs.Remove(job.Number);
        }
    }
}
