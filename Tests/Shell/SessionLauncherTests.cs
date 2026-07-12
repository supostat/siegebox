using NUnit.Framework;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins launch identity: a session opens under a configured user (unprivileged by
    /// default), rooted at that user's home, and root is only an explicit choice. A missing
    /// user is a fatal misconfiguration, not a silent fallback to root.
    /// </summary>
    [TestFixture]
    public sealed class SessionLauncherTests
    {
        private static AuthenticationService SeededAuth()
        {
            var vfs = new VirtualFileSystem();
            UserSeed.Seed(vfs);
            return new AuthenticationService(vfs);
        }

        [Test]
        public void Session_opens_under_the_configured_unprivileged_identity()
        {
            var session = SessionLauncher.OpenFor(SeededAuth(), UserSeed.PlayerName);

            Assert.That(session.Credentials.Uid, Is.EqualTo(UserSeed.PlayerUid));
            Assert.That(session.Credentials.IsRoot, Is.False);
            Assert.That(session.WorkingDirectory, Is.EqualTo(UserSeed.PlayerHome));
        }

        [Test]
        public void Root_is_available_as_an_explicit_choice()
        {
            var session = SessionLauncher.OpenFor(SeededAuth(), "root");

            Assert.That(session.Credentials.IsRoot, Is.True);
            Assert.That(session.WorkingDirectory, Is.EqualTo("/root"));
        }

        [Test]
        public void Opening_a_session_for_an_unknown_user_fails_loudly()
        {
            Assert.That(
                () => SessionLauncher.OpenFor(SeededAuth(), "ghost"),
                Throws.InstanceOf<UserDatabaseException>());
        }
    }
}
