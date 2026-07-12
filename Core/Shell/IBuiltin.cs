using System.Collections.Generic;

namespace Siegebox.Shell
{
    /// <summary>
    /// A command that mutates the live session instead of only observing a context snapshot.
    /// In subshell positions (inside a pipe, background) it receives a session clone.
    /// <paramref name="inputLine"/> is null on the first call; a builtin that returns
    /// <see cref="BuiltinResult.ReadLine"/> is re-invoked with the line read from stdin.
    /// </summary>
    public interface IBuiltin
    {
        string Name { get; }

        BuiltinResult Execute(ShellSession session, IReadOnlyList<string> arguments, string? inputLine);
    }
}
