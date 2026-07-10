using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class CommandNode : IShellAstNode
    {
        public CommandNode(IReadOnlyList<string> words, IReadOnlyList<Redirection> redirections)
        {
            if (words is null)
            {
                throw new ArgumentNullException(nameof(words));
            }

            if (words.Count == 0)
            {
                throw new ArgumentException("A command needs at least one word.", nameof(words));
            }

            if (redirections is null)
            {
                throw new ArgumentNullException(nameof(redirections));
            }

            Words = words;
            Redirections = redirections;
        }

        public IReadOnlyList<string> Words { get; }

        public IReadOnlyList<Redirection> Redirections { get; }
    }
}
