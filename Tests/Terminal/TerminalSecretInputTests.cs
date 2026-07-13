using NUnit.Framework;
using Siegebox.Shell.Tests;

namespace Siegebox.Terminal.Tests
{
    /// <summary>
    /// Pins the terminal's no-echo secret input: a prompt carrying the secret marker suppresses
    /// the echo of the line typed in response (only the Enter is echoed, never the secret chars),
    /// re-arms per prompt, clears when the reader goes idle, and never triggers off stderr.
    /// </summary>
    [TestFixture]
    public sealed class TerminalSecretInputTests
    {
        [Test]
        public void Secret_line_is_hidden_but_the_prompt_and_its_newline_survive()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("secret", "Enter: "));

            harness.RunLine("secret");

            Assert.That(harness.Session.EchoSuppressed, Is.True);
            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # secret\nEnter: "));

            harness.Session.SubmitLine("hunter2");
            harness.TickAndPump();

            Assert.That(harness.Session.EchoSuppressed, Is.False);
            Assert.That(harness.Session.ScrollbackText, Does.Not.Contain("hunter2"));
            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # secret\nEnter: \n"));
        }

        [Test]
        public void Empty_secret_still_echoes_the_enter_keypress()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("secret", "Enter: "));
            harness.RunLine("secret");

            harness.Session.SubmitLine("");
            harness.TickAndPump();

            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # secret\nEnter: \n"));
            Assert.That(harness.Session.EchoSuppressed, Is.False);
        }

        [Test]
        public void Marker_on_stderr_does_not_suppress_echo()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("leak", "Enter: ", consumesLine: true, onStandardError: true));

            harness.RunLine("leak");

            Assert.That(harness.Session.IsBusy, Is.True);
            Assert.That(harness.Session.EchoSuppressed, Is.False);
        }

        [Test]
        public void Marker_on_stderr_never_renders_in_scrollback()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("leak", "Enter: ", consumesLine: true, onStandardError: true));

            harness.RunLine("leak");

            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # leak\nEnter: "));
        }

        [Test]
        public void Last_submit_was_secret_is_true_only_for_a_suppressed_secret_line()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("secret", "Enter: "));

            harness.RunLine("secret");
            Assert.That(harness.Session.LastSubmitWasSecret, Is.False, "the idle submit of the command itself is not secret");

            harness.Session.SubmitLine("hunter2");
            harness.TickAndPump();

            Assert.That(harness.Session.LastSubmitWasSecret, Is.True);
        }

        [Test]
        public void Last_submit_was_secret_is_false_for_a_normal_busy_line()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new ByteReaderCommand("reader"));

            harness.RunLine("reader");
            Assert.That(harness.Session.IsBusy, Is.True);

            harness.Session.SubmitLine("plain");
            harness.TickAndPump();

            Assert.That(harness.Session.LastSubmitWasSecret, Is.False);
        }

        [Test]
        public void Secret_line_submitted_before_its_prompt_is_pumped_is_still_suppressed()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("secret", "Enter: "));

            harness.Session.SubmitLine("secret");

            // Advance the process so it writes its prompt+marker into the output pipe, but do NOT
            // pump: the marker stays undrained and EchoSuppressed is still false — the type-ahead race.
            harness.Scheduler.Tick();
            Assert.That(harness.Session.IsBusy, Is.True);
            Assert.That(harness.Session.EchoSuppressed, Is.False);

            harness.Session.SubmitLine("hunter2");
            harness.TickAndPump();

            Assert.That(harness.Session.LastSubmitWasSecret, Is.True);
            Assert.That(harness.Session.ScrollbackText, Does.Not.Contain("hunter2"));
        }

        [Test]
        public void Reader_that_dies_without_consuming_clears_suppression_when_idle()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SecretPromptCommand("flash", "Enter: ", consumesLine: false));

            harness.RunLine("flash");

            Assert.That(harness.Session.IsBusy, Is.False);
            Assert.That(harness.Session.ScrollbackText, Does.Contain("Enter: "));
            Assert.That(harness.Session.EchoSuppressed, Is.False);
        }

        [Test]
        public void Su_password_prompt_hides_the_typed_password()
        {
            var harness = new TerminalHarness(uid: 1000);
            harness.SeedUsers();

            harness.RunLine("su root");
            Assert.That(harness.Session.EchoSuppressed, Is.True);
            Assert.That(harness.Session.ScrollbackText, Does.Contain("Password: "));

            harness.Session.SubmitLine("wrongpass");
            harness.TickAndPump();

            var scrollback = harness.Session.ScrollbackText;
            Assert.That(scrollback, Does.Contain("Password: "));
            Assert.That(scrollback, Does.Not.Contain("wrongpass"));
            Assert.That(scrollback, Does.Contain("authentication failure"));
            Assert.That(harness.Session.EchoSuppressed, Is.False);
        }

        [Test]
        public void Passwd_hides_every_password_and_re_arms_between_prompts()
        {
            var harness = new TerminalHarness(uid: 1000);
            harness.SeedUsers();
            harness.SeedBin();

            harness.RunLine("passwd");
            Assert.That(harness.Session.ScrollbackText, Does.Contain("Current password: "));
            Assert.That(harness.Session.EchoSuppressed, Is.True);

            harness.Session.SubmitLine("player");
            harness.TickAndPump();
            Assert.That(harness.Session.ScrollbackText, Does.Contain("New password: "));
            Assert.That(harness.Session.EchoSuppressed, Is.True);

            harness.Session.SubmitLine("changed");
            harness.TickAndPump();
            Assert.That(harness.Session.ScrollbackText, Does.Contain("Retype new password: "));
            Assert.That(harness.Session.EchoSuppressed, Is.True);

            harness.Session.SubmitLine("changed");
            harness.TickAndPump();

            var scrollback = harness.Session.ScrollbackText;
            Assert.That(scrollback, Does.Contain("Current password: "));
            Assert.That(scrollback, Does.Contain("New password: "));
            Assert.That(scrollback, Does.Contain("Retype new password: "));
            Assert.That(scrollback, Does.Not.Contain("player"));
            Assert.That(scrollback, Does.Not.Contain("changed"));
            Assert.That(harness.Session.EchoSuppressed, Is.False);
        }
    }
}
