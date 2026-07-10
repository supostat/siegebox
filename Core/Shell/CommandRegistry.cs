using System;
using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class CommandRegistry
    {
        private readonly Dictionary<string, ICommand> commands = new Dictionary<string, ICommand>(StringComparer.Ordinal);

        public void Register(ICommand command)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (commands.ContainsKey(command.Name))
            {
                throw new ArgumentException($"Command '{command.Name}' is already registered.", nameof(command));
            }

            commands.Add(command.Name, command);
        }

        public bool TryGet(string name, out ICommand command)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return commands.TryGetValue(name, out command!);
        }

        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>(commands.Keys);
                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }
    }
}
