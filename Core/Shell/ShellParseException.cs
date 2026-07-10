using System;

namespace Siegebox.Shell
{
    public sealed class ShellParseException : Exception
    {
        public ShellParseException(string message, int position)
            : base(message)
        {
            Position = position;
        }

        public int Position { get; }
    }
}
