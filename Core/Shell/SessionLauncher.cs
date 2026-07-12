using System;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// The single "open a session as whom" seam: resolves a user by name in the db and opens
    /// a <see cref="ShellSession"/> under that identity, rooted at their home. This is where
    /// launch identity is chosen — the composition root opens the starting terminal as an
    /// unprivileged user, root only as a deliberate call. A missing user is a fatal
    /// misconfiguration, not a silent fallback to root.
    /// </summary>
    public static class SessionLauncher
    {
        public static ShellSession OpenFor(AuthenticationService authentication, string userName)
        {
            if (authentication is null)
            {
                throw new ArgumentNullException(nameof(authentication));
            }

            if (userName is null)
            {
                throw new ArgumentNullException(nameof(userName));
            }

            if (!authentication.TryResolveByName(userName, out var user))
            {
                throw new UserDatabaseException($"cannot open a session: user '{userName}' does not exist");
            }

            return new ShellSession(user.Home, new Credentials(user.Uid, user.Gid));
        }
    }
}
