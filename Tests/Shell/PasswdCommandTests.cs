using NUnit.Framework;
using Siegebox.Security;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins the passwd command with its policy keyed on the REAL identity: a user changes only
    /// their own password (old verified), root changes anyone's without the old, a wrong old or
    /// a mismatch leaves shadow untouched, and — the security-critical case — a setuid-elevated
    /// player is REFUSED when targeting root, because effective-root never buys real-root policy.
    /// </summary>
    [TestFixture]
    public sealed class PasswdCommandTests
    {
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
        public void A_user_changes_their_own_password_with_the_correct_old()
        {
            var harness = SeededPlayer();
            harness.FeedInput("player\nnewpw\nnewpw\n");

            harness.Run("passwd");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(CanAuthenticate(harness, "player", "newpw"), Is.True);
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.False);
        }

        [Test]
        public void A_wrong_old_password_leaves_the_shadow_untouched()
        {
            var harness = SeededPlayer();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);
            harness.FeedInput("wrongold\nnewpw\nnewpw\n");

            harness.Run("passwd");

            Assert.That(harness.DrainError(), Does.Contain("authentication failure"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.True);
        }

        [Test]
        public void Root_changes_another_users_password_without_the_old()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            harness.FeedInput("secret\nsecret\n");

            harness.Run("passwd player");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(CanAuthenticate(harness, "player", "secret"), Is.True);
        }

        [Test]
        public void A_setuid_elevated_player_cannot_change_the_root_password()
        {
            var harness = SeededPlayer();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);

            harness.Run("passwd root");

            Assert.That(harness.DrainError(), Does.Contain("you may not change the password for root"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
            Assert.That(CanAuthenticate(harness, "root", "root"), Is.True);
        }

        [Test]
        public void A_mismatched_new_password_is_rejected()
        {
            var harness = SeededPlayer();
            harness.FeedInput("player\nnew1\nnew2\n");

            harness.Run("passwd");

            Assert.That(harness.DrainError(), Does.Contain("passwords do not match"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.True);
        }

        [Test]
        public void An_unknown_target_user_is_reported_and_shadow_is_untouched()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);

            harness.Run("passwd ghost");

            Assert.That(harness.DrainError(), Does.Contain("passwd: user 'ghost' does not exist"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.True);
        }

        [Test]
        public void Too_many_arguments_are_reported_and_shadow_is_untouched()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);

            harness.Run("passwd a b");

            Assert.That(harness.DrainError(), Does.Contain("passwd: too many arguments"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.True);
        }

        [Test]
        public void An_empty_new_password_leaves_the_password_unchanged()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);
            harness.FeedInput("\n\n");

            harness.Run("passwd player");

            Assert.That(harness.DrainError(), Does.Contain("passwd: password unchanged"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.True);
        }

        [Test]
        public void Passwd_blocks_on_a_partial_line_then_resumes_when_the_rest_arrives()
        {
            var harness = SeededPlayer();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);

            harness.Shell.Execute("passwd");
            harness.Tick(ShellHarness.DefaultMaxTicks);
            harness.FeedInput("play");
            harness.Tick(ShellHarness.DefaultMaxTicks);

            Assert.That(harness.Scheduler.ProcessCount, Is.GreaterThan(0));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));

            harness.FeedInput("er\nnewpw\nnewpw\n");
            harness.RunUntilIdle();

            Assert.That(CanAuthenticate(harness, "player", "newpw"), Is.True);
            Assert.That(CanAuthenticate(harness, "player", "player"), Is.False);
        }

        [Test]
        public void An_unresolvable_caller_cannot_determine_the_user()
        {
            var harness = new ShellHarness(uid: 4242);
            harness.SeedUsers();
            var shadowBefore = harness.ReadFile(AuthenticationService.ShadowPath);

            harness.Run("passwd");

            Assert.That(harness.DrainError(), Does.Contain("passwd: cannot determine the caller's user name"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.ReadFile(AuthenticationService.ShadowPath), Is.EqualTo(shadowBefore));
        }
    }
}
