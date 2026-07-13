using System;

namespace Siegebox.App
{
    /// <summary>One command listed in the doc browser: its name and a one-line description.</summary>
    public sealed class DocEntry
    {
        public DocEntry(string commandName, string description)
        {
            CommandName = commandName ?? throw new ArgumentNullException(nameof(commandName));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        public string CommandName { get; }

        public string Description { get; }
    }
}
