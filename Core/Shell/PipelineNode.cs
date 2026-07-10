using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class PipelineNode : IShellAstNode
    {
        public PipelineNode(IReadOnlyList<CommandNode> commands, bool background)
        {
            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (commands.Count == 0)
            {
                throw new ArgumentException("A pipeline needs at least one command.", nameof(commands));
            }

            Commands = commands;
            Background = background;
        }

        public IReadOnlyList<CommandNode> Commands { get; }

        public bool Background { get; }
    }
}
