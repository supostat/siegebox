using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class PsCommand : ICommand
    {
        private readonly Scheduler scheduler;

        public PsCommand(Scheduler scheduler)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public string Name => "ps";

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

            return new PsProcess(context, scheduler);
        }

        private sealed class PsProcess : BufferedCommandProcess
        {
            private readonly Scheduler scheduler;

            public PsProcess(ExecutionContext context, Scheduler scheduler)
                : base(context)
            {
                this.scheduler = scheduler;
            }

            protected override string CommandName => "ps";

            protected override CommandOutcome Run()
            {
                var output = new StringBuilder();
                foreach (var process in scheduler.ListProcesses())
                {
                    output.Append($"{process.Pid} {process.State} {process.Name}\n");
                }

                return CommandOutcome.Ok(output.ToString());
            }
        }
    }
}
