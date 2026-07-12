using System;
using System.Collections.Generic;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Switches the session identity to another user resolved by name in the user db
    /// (<c>su [user]</c>, default <c>root</c>). Root switches to anyone without a password;
    /// every other user must supply the target's password, prompted on stdin and verified
    /// against /etc/shadow through the trusted <see cref="AuthenticationService"/>. A wrong
    /// password or an unknown user fails with exit 1 and leaves the identity unchanged.
    /// Password echo is not yet suppressed by the terminal — a known limitation.
    /// </summary>
    public sealed class SuBuiltin : IBuiltin
    {
        private const string DefaultTarget = "root";

        private readonly AuthenticationService authentication;

        public SuBuiltin(AuthenticationService authentication)
        {
            this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
        }

        public string Name => "su";

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
                return BuiltinResult.Completed(1, "", "su: too many arguments\n");
            }

            var targetName = arguments.Count == 0 ? DefaultTarget : arguments[0];
            if (!authentication.TryResolveByName(targetName, out var target))
            {
                return BuiltinResult.Completed(1, "", $"su: user '{targetName}' does not exist\n");
            }

            if (session.Credentials.IsRoot)
            {
                return SwitchTo(session, target);
            }

            if (inputLine == null)
            {
                return BuiltinResult.ReadLine("Password: ");
            }

            if (!authentication.Authenticate(targetName, inputLine))
            {
                return BuiltinResult.Completed(1, "", "su: authentication failure\n");
            }

            return SwitchTo(session, target);
        }

        private static BuiltinResult SwitchTo(ShellSession session, UserRecord target)
        {
            session.Credentials = new Credentials(target.Uid, target.Gid);
            return BuiltinResult.Completed(0);
        }
    }
}
