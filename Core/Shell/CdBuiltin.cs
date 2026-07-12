using System;
using System.Collections.Generic;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class CdBuiltin : IBuiltin
    {
        private readonly VirtualFileSystem vfs;

        public CdBuiltin(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "cd";

        public BuiltinResult Execute(ShellSession session, IReadOnlyList<string> arguments, string? inputLine)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (arguments.Count > 1)
            {
                return BuiltinResult.Completed(1, "", "cd: too many arguments\n");
            }

            var target = arguments.Count == 1 ? arguments[0] : DefaultTargetOf(session);
            var absolute = ShellPath.Absolute(session.WorkingDirectory, target);
            session.WorkingDirectory = vfs.ResolveDirectoryPath(absolute, session.Credentials);
            return BuiltinResult.Completed(0);
        }

        private static string DefaultTargetOf(ShellSession session)
            => session.Environment.TryGetValue("HOME", out var home) && home.Length > 0 ? home : "/";
    }
}
