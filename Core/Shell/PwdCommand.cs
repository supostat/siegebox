using System;
using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class PwdCommand : ICommand
    {
        public string Name => "pwd";

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

            return new PwdProcess(context);
        }

        private sealed class PwdProcess : BufferedCommandProcess
        {
            public PwdProcess(ExecutionContext context)
                : base(context)
            {
            }

            protected override string CommandName => "pwd";

            protected override CommandOutcome Run() => CommandOutcome.Ok(Context.WorkingDirectory + "\n");
        }
    }
}
