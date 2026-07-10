using System;
using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class EchoCommand : ICommand
    {
        public string Name => "echo";

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

            return new EchoProcess(context, arguments);
        }

        private sealed class EchoProcess : BufferedCommandProcess
        {
            private readonly IReadOnlyList<string> arguments;

            public EchoProcess(ExecutionContext context, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.arguments = arguments;
            }

            protected override string CommandName => "echo";

            protected override CommandOutcome Run() => CommandOutcome.Ok(string.Join(" ", arguments) + "\n");
        }
    }
}
