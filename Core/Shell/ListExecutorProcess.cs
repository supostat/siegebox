using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Walks one parsed command line: launches each pipeline, waits for every member of a
    /// foreground pipeline (the last member's status becomes <c>$?</c>), backgrounds jobs with
    /// a "[n] pid" announcement, and applies &amp;&amp; / || short-circuiting.
    /// </summary>
    internal sealed class ListExecutorProcess : IProcess
    {
        private const int BurnedStatusExitCode = 127;

        private readonly ListNode list;
        private readonly ShellSession session;
        private readonly Scheduler scheduler;
        private readonly PipelineAssembler assembler;
        private readonly JobTable jobs;
        private readonly IByteStream terminalInput;
        private readonly IByteStream terminalOutput;
        private readonly IByteStream terminalError;
        private PendingWrite? announcement;
        private IReadOnlyList<int>? awaitingPids;
        private int awaitingIndex;
        private int lastCollectedExitCode;
        private int itemIndex;

        public ListExecutorProcess(
            ListNode list,
            ShellSession session,
            Scheduler scheduler,
            PipelineAssembler assembler,
            JobTable jobs,
            ExecutionContext ownContext)
        {
            this.list = list;
            this.session = session;
            this.scheduler = scheduler;
            this.assembler = assembler;
            this.jobs = jobs;
            Context = ownContext;
            terminalInput = ownContext.FileDescriptors.Get(FileDescriptorTable.Stdin);
            terminalOutput = ownContext.FileDescriptors.Get(FileDescriptorTable.Stdout);
            terminalError = ownContext.FileDescriptors.Get(FileDescriptorTable.Stderr);
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public ProcessState Step()
        {
            if (announcement is not null)
            {
                switch (announcement.Advance())
                {
                    case DrainStatus.WouldBlock:
                        WakeCondition = WakeCondition.Writable(terminalError);
                        return ProcessState.Sleeping;
                    case DrainStatus.Closed:
                        return FinishWithLastExitCode();
                    default:
                        announcement = null;
                        break;
                }
            }

            if (awaitingPids is not null && !TryCollectForeground())
            {
                WakeCondition = WakeCondition.ProcessExit(awaitingPids[awaitingIndex]);
                return ProcessState.Sleeping;
            }

            if (itemIndex == list.Items.Count)
            {
                return FinishWithLastExitCode();
            }

            return RunCurrentItem();
        }

        /// <summary>
        /// A foreground pipeline is waited on member by member (POSIX: the shell waits for the
        /// whole pipeline), so every retained status is collected and none leaks; <c>$?</c> is
        /// the last member's code. A member whose status someone else already collected (burned)
        /// and that is gone from the table resolves to 127 instead of wedging the executor.
        /// </summary>
        private bool TryCollectForeground()
        {
            while (awaitingIndex < awaitingPids!.Count)
            {
                var awaitedPid = awaitingPids[awaitingIndex];
                if (!scheduler.TryCollectExitCode(awaitedPid, out lastCollectedExitCode))
                {
                    if (scheduler.Contains(awaitedPid))
                    {
                        return false;
                    }

                    lastCollectedExitCode = BurnedStatusExitCode;
                }

                awaitingIndex++;
            }

            session.LastExitCode = lastCollectedExitCode;
            awaitingPids = null;
            awaitingIndex = 0;
            itemIndex++;
            return true;
        }

        private ProcessState RunCurrentItem()
        {
            var item = list.Items[itemIndex];
            if (!ShouldRun(item.Operator))
            {
                itemIndex++;
                return ProcessState.Running;
            }

            var launched = assembler.Launch(item.Pipeline, session, terminalInput, terminalOutput, terminalError);
            if (item.Pipeline.Background)
            {
                var job = jobs.Add(launched.Pids, launched.LastPid, launched.Description);
                announcement = new PendingWrite(terminalError, Encoding.UTF8.GetBytes($"[{job.Number}] {launched.LastPid}\n"));
                session.LastExitCode = 0;
                itemIndex++;
                return ProcessState.Running;
            }

            awaitingPids = launched.Pids;
            awaitingIndex = 0;
            WakeCondition = WakeCondition.ProcessExit(awaitingPids[0]);
            return ProcessState.Sleeping;
        }

        private bool ShouldRun(ListOperator chainOperator) => chainOperator switch
        {
            ListOperator.AndIf => session.LastExitCode == 0,
            ListOperator.OrIf => session.LastExitCode != 0,
            _ => true
        };

        private ProcessState FinishWithLastExitCode()
        {
            ExitCode = session.LastExitCode;
            return ProcessState.Finished;
        }
    }
}
