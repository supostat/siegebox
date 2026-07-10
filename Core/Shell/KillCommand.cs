using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class KillCommand : ICommand
    {
        private readonly Scheduler scheduler;

        public KillCommand(Scheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public string Name => "kill";

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return new KillProcess(context, scheduler, arguments);
        }

        private sealed class KillProcess : BufferedCommandProcess
        {
            private readonly Scheduler scheduler;
            private readonly IReadOnlyList<string> arguments;

            public KillProcess(ExecutionContext context, Scheduler scheduler, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.scheduler = scheduler;
                this.arguments = arguments;
            }

            protected override string CommandName => "kill";

            protected override CommandOutcome Run()
            {
                if (arguments.Count == 0)
                {
                    return CommandOutcome.Fail(1, "kill: usage: kill pid...\n");
                }

                var ownerUidsByPid = OwnerUidsByPid();
                var error = new StringBuilder();
                foreach (var argument in arguments)
                {
                    if (!int.TryParse(argument, out var pid))
                    {
                        error.Append($"kill: {argument}: arguments must be process ids\n");
                        continue;
                    }

                    if (!ownerUidsByPid.TryGetValue(pid, out var ownerUid))
                    {
                        error.Append($"kill: {pid}: No such process\n");
                        continue;
                    }

                    if (!MayKill(ownerUid))
                    {
                        error.Append($"kill: ({pid}) - Operation not permitted\n");
                        continue;
                    }

                    scheduler.Kill(pid);
                }

                return error.Length == 0 ? CommandOutcome.Ok() : CommandOutcome.Fail(1, error.ToString());
            }

            private bool MayKill(int targetOwnerUid)
                => Context.Credentials.IsRoot || targetOwnerUid == Context.Credentials.Uid;

            private Dictionary<int, int> OwnerUidsByPid()
            {
                var owners = new Dictionary<int, int>();
                foreach (var process in scheduler.ListProcesses())
                {
                    owners[process.Pid] = process.OwnerUid;
                }

                return owners;
            }
        }
    }
}
