using NUnit.Framework;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins su authentication: a non-root user must supply the target's password (read from
    /// stdin), a wrong password or unknown user leaves the identity unchanged, root switches
    /// to anyone without a password, and su in a pipe still mutates only the session clone.
    /// </summary>
    [TestFixture]
    public sealed class SuBuiltinTests
    {
        private static ShellHarness Unprivileged()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.SeedUsers();
            return harness;
        }

        [Test]
        public void Su_with_correct_password_switches_identity()
        {
            var harness = Unprivileged();
            harness.FeedInput("root\n");

            harness.Run("su root");

            Assert.That(harness.Session.Credentials.IsRoot, Is.True);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Su_with_wrong_password_keeps_identity_and_fails()
        {
            var harness = Unprivileged();
            harness.FeedInput("wrong\n");

            harness.Run("su root");

            Assert.That(harness.Session.Credentials.Uid, Is.EqualTo(1000));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("authentication failure"));
        }

        [Test]
        public void Su_root_to_user_needs_no_password()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();

            harness.Run("su player");

            Assert.That(harness.Session.Credentials.Uid, Is.EqualTo(1000));
            Assert.That(harness.Session.Credentials.IsRoot, Is.False);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Su_unknown_user_fails_and_leaves_the_identity_unchanged()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();

            harness.Run("su ghost");

            Assert.That(harness.Session.Credentials.IsRoot, Is.True);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("does not exist"));
        }

        [Test]
        public void Su_in_pipe_still_mutates_only_the_clone()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();

            harness.Run("su player | cat");

            Assert.That(harness.Session.Credentials.IsRoot, Is.True);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Su_reads_exactly_one_line_and_leaves_the_rest_of_the_input()
        {
            var harness = Unprivileged();
            harness.FeedInput("root\nleftover\n");

            harness.Run("su root");

            Assert.That(harness.Session.Credentials.IsRoot, Is.True);
            Assert.That(harness.DrainInput(), Is.EqualTo("leftover\n"));
        }

        [Test]
        public void Su_with_a_carriage_return_in_the_password_still_succeeds()
        {
            var harness = Unprivileged();
            harness.FeedInput("root\r\n");

            harness.Run("su root");

            Assert.That(harness.Session.Credentials.IsRoot, Is.True);
        }

        [Test]
        public void Su_with_an_empty_password_fails_and_keeps_the_identity()
        {
            var harness = Unprivileged();
            harness.FeedInput("\n");

            harness.Run("su root");

            Assert.That(harness.Session.Credentials.Uid, Is.EqualTo(1000));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("authentication failure"));
        }

        [Test]
        public void Su_with_too_many_arguments_fails()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();

            harness.Run("su player root");

            Assert.That(harness.Session.Credentials.IsRoot, Is.True);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("too many arguments"));
        }

        [Test]
        public void Dropping_privilege_then_a_root_only_operation_fails()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();

            harness.Run("su player");
            harness.Run("mkdir /nope");

            Assert.That(harness.Session.Credentials.Uid, Is.EqualTo(1000));
            Assert.That(harness.DrainError(), Does.Contain("mkdir: /nope: Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }
    }
}
