using System.Collections.Generic;
using Siegebox.Process;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Spawns a <see cref="SecretPromptProcess"/> that writes the secret-prompt marker and a
    /// prompt (on stdout, or stderr when <c>onStandardError</c>), optionally reading one line.
    /// </summary>
    internal sealed class SecretPromptCommand : ICommand
    {
        private readonly string prompt;
        private readonly bool consumesLine;
        private readonly bool onStandardError;

        public SecretPromptCommand(string name, string prompt, bool consumesLine = true, bool onStandardError = false)
        {
            Name = name;
            this.prompt = prompt;
            this.consumesLine = consumesLine;
            this.onStandardError = onStandardError;
        }

        public string Name { get; }

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
            => new SecretPromptProcess(context, prompt, onStandardError, consumesLine);
    }
}
