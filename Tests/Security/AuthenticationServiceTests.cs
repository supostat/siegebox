using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Security.Tests
{
    /// <summary>
    /// Pins the trusted auth door over a seeded VFS: shadow is root-only (an unprivileged
    /// process gets EACCES), while the service itself resolves users and verifies passwords
    /// against the seeded hashes. A missing user or a wrong password fails closed.
    /// </summary>
    [TestFixture]
    public sealed class AuthenticationServiceTests
    {
        private static readonly Credentials Player = new Credentials(UserSeed.PlayerUid);

        private static AuthenticationService SeededAuth(out VirtualFileSystem vfs)
        {
            vfs = new VirtualFileSystem();
            UserSeed.Seed(vfs);
            return new AuthenticationService(vfs);
        }

        [Test]
        public void Shadow_is_denied_to_an_unprivileged_reader()
        {
            SeededAuth(out var vfs);

            Assert.That(
                () => vfs.Open(AuthenticationService.ShadowPath, OpenMode.Read, Player),
                Throws.InstanceOf<VfsException>().With.Property("Error").EqualTo(VfsError.EACCES));
            Assert.That(
                () => vfs.Open(AuthenticationService.PasswdPath, OpenMode.Read, Player).CloseRead(),
                Throws.Nothing);
        }

        [Test]
        public void Authenticate_accepts_the_correct_password()
        {
            var auth = SeededAuth(out _);

            Assert.That(auth.Authenticate("player", "player"), Is.True);
            Assert.That(auth.Authenticate("root", "root"), Is.True);
        }

        [Test]
        public void Authenticate_rejects_a_wrong_password_or_unknown_user()
        {
            var auth = SeededAuth(out _);

            Assert.That(auth.Authenticate("player", "wrong"), Is.False);
            Assert.That(auth.Authenticate("ghost", "player"), Is.False);
        }

        [Test]
        public void Resolve_returns_the_record_for_a_known_user_and_misses_an_unknown_one()
        {
            var auth = SeededAuth(out _);

            Assert.That(auth.TryResolveByName("player", out var player), Is.True);
            Assert.That(player.Uid, Is.EqualTo(UserSeed.PlayerUid));
            Assert.That(player.Home, Is.EqualTo(UserSeed.PlayerHome));
            Assert.That(auth.TryResolveByName("ghost", out _), Is.False);
        }
    }
}
