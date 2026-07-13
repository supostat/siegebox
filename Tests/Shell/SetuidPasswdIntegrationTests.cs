using NUnit.Framework;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// The whole line, end to end: an unprivileged process legitimately writes root-only
    /// /etc/shadow ONLY because /usr/bin/passwd carries the setuid bit — clearing that bit
    /// breaks the write with EACCES and leaves shadow untouched, proving access flows from the
    /// visible file property and not from any ambient escalation.
    /// </summary>
    [TestFixture]
    public sealed class SetuidPasswdIntegrationTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static ShellHarness SeededPlayer()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.SeedUsers();
            harness.SeedBin();
            return harness;
        }

        private static bool CanAuthenticate(ShellHarness harness, string user, string password)
            => new AuthenticationService(harness.Vfs).Authenticate(user, password);

        [Test]
        public void Passwd_as_a_setuid_tool_writes_shadow_as_uid_1000()
        {
            var harness = SeededPlayer();
            harness.FeedInput("player\nchanged\nchanged\n");

            harness.Run("passwd");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(CanAuthenticate(harness, "player", "changed"), Is.True);
        }

        [Test]
        public void Removing_the_setuid_bit_breaks_the_write_with_EACCES()
        {
            var harness = SeededPlayer();
            harness.Vfs.Chmod("/usr/bin/passwd", new PermissionMode(0b111_101_101), Root);
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);
            harness.FeedInput("player\nchanged\nchanged\n");

            harness.Run("passwd");

            var error = harness.DrainError();
            Assert.That(error, Does.Contain(AuthenticationService.ShadowPath));
            Assert.That(error, Does.Contain("Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.True);
        }
    }
}
