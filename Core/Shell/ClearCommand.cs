using System;
using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class ClearCommand : ICommand
    {
        private const char Escape = (char)0x1B;

        public static readonly string ClearSequence = Escape + "[2J" + Escape + "[H";

        public string Name => "clear";

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

            return new ClearProcess(context);
        }

        private sealed class ClearProcess : BufferedCommandProcess
        {
            public ClearProcess(ExecutionContext context)
                : base(context)
            {
            }

            protected override string CommandName => "clear";

            protected override CommandOutcome Run() => CommandOutcome.Ok(ClearSequence);
        }
    }
}
