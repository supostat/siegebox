using System;
using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class HelpCommand : ICommand
    {
        private readonly CommandRegistry commands;
        private readonly BuiltinRegistry builtins;

        public HelpCommand(CommandRegistry commands, BuiltinRegistry builtins)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.builtins = builtins ?? throw new ArgumentNullException(nameof(builtins));
        }

        public string Name => "help";

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

            return new HelpProcess(context, commands, builtins);
        }

        private sealed class HelpProcess : BufferedCommandProcess
        {
            private readonly CommandRegistry commands;
            private readonly BuiltinRegistry builtins;

            public HelpProcess(ExecutionContext context, CommandRegistry commands, BuiltinRegistry builtins)
                : base(context)
            {
                this.commands = commands;
                this.builtins = builtins;
            }

            protected override string CommandName => "help";

            protected override CommandOutcome Run()
                => CommandOutcome.Ok(
                    "builtins: " + string.Join(" ", builtins.Names) + "\n" +
                    "commands: " + string.Join(" ", commands.Names) + "\n");
        }
    }
}
