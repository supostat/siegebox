using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// <c>passwd [user]</c>: interactively changes a password by rewriting /etc/shadow. It is a
    /// registry command (not a builtin) so the setuid-root /usr/bin/passwd file elevates its
    /// effective identity to root — the only reason an unprivileged caller can write the
    /// root-only shadow. Authorization and the old-password prompt are keyed on the REAL
    /// identity, never the effective one, so a setuid-elevated player may change only their own
    /// password. Password echo is not suppressed by the terminal — a known limitation.
    /// </summary>
    public sealed class PasswdCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;
        private readonly AuthenticationService authentication;

        public PasswdCommand(VirtualFileSystem vfs, AuthenticationService authentication)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        }

        public string Name => "passwd";

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

            return new PasswdProcess(context, vfs, authentication, arguments);
        }
    }
}
