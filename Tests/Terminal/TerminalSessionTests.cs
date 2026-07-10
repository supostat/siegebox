using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Siegebox.Shell.Tests;
using Siegebox.Terminal;

namespace Siegebox.Terminal.Tests
{
    [TestFixture]
    public sealed class TerminalSessionTests
    {
        private static int AnnouncedPid(string scrollback)
        {
            var match = Regex.Match(scrollback, @"\[1\] (\d+)");
            Assert.That(match.Success, Is.True, "no job announcement in scrollback");
            return int.Parse(match.Groups[1].Value);
        }

        [Test]
        public void Prompt_echo_and_full_output_land_in_the_scrollback()
        {
            var harness = new TerminalHarness();

            harness.RunLine("echo hi");

            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # echo hi\nhi\n"));
            Assert.That(harness.Session.IsBusy, Is.False);
        }

        [Test]
        public void Stderr_lands_in_the_scrollback()
        {
            var harness = new TerminalHarness();

            harness.RunLine("cat /missing");

            Assert.That(harness.Session.ScrollbackText, Does.Contain("cat: /missing: No such file or directory\n"));
        }

        [Test]
        public void Busy_line_routes_to_stdin_and_round_trips_through_cat()
        {
            var harness = new TerminalHarness();
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);

            var accepted = harness.RunLine("hello", ticks: 4);

            Assert.That(accepted, Is.True);
            Assert.That(harness.Session.ScrollbackText, Does.Contain("hello\nhello\n"));
            Assert.That(harness.Session.IsBusy, Is.True);
        }

        [Test]
        public void Pump_without_new_output_does_not_bump_the_scrollback_version()
        {
            var harness = new TerminalHarness();
            harness.RunLine("echo hi");
            var versionAfterOutput = harness.Session.ScrollbackVersion;

            harness.TickAndPump(4);

            Assert.That(harness.Session.ScrollbackVersion, Is.EqualTo(versionAfterOutput));
        }

        [Test]
        public void Busy_lines_flow_fifo_and_echo_without_a_prompt()
        {
            var harness = new TerminalHarness();
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);

            harness.Session.SubmitLine("one");
            harness.Session.SubmitLine("two");
            harness.TickAndPump(4);

            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # cat\none\ntwo\none\ntwo\n"));
        }

        [Test]
        public void Line_at_the_pending_cap_boundary_is_accepted()
        {
            var harness = new TerminalHarness();
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);

            var exactFit = new string('a', TerminalSession.MaxPendingInputBytes - 1);

            Assert.That(harness.Session.SubmitLine(exactFit), Is.True);
        }

        [Test]
        public void Blank_line_echoes_the_prompt_and_stays_idle()
        {
            var harness = new TerminalHarness();

            var accepted = harness.RunLine("");

            Assert.That(accepted, Is.True);
            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # \n"));
            Assert.That(harness.Session.IsBusy, Is.False);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Finished_background_job_is_announced_done_and_its_status_collected()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new ProbeCommand("exit3", 3));

            harness.RunLine("exit3 &");

            var scrollback = harness.Session.ScrollbackText;
            var probePid = AnnouncedPid(scrollback);
            Assert.That(scrollback, Does.Contain($"[1] {probePid} Done exit3\n"));
            Assert.That(harness.Jobs.Jobs, Is.Empty);
            Assert.That(harness.Scheduler.TryPeekExitCode(probePid, out _), Is.False);
        }

        [Test]
        public void Session_is_idle_between_commands()
        {
            var harness = new TerminalHarness();

            harness.RunLine("echo one");
            Assert.That(harness.Session.IsBusy, Is.False);

            harness.RunLine("echo two");
            Assert.That(harness.Session.ScrollbackText, Does.Contain("one\n").And.Contain("two\n"));
        }

        [Test]
        public void Oversized_pending_line_is_rejected()
        {
            var harness = new TerminalHarness();
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);
            var versionBefore = harness.Session.ScrollbackVersion;

            var accepted = harness.Session.SubmitLine(new string('a', TerminalSession.MaxPendingInputBytes));

            Assert.That(accepted, Is.False);
            Assert.That(harness.Session.ScrollbackVersion, Is.EqualTo(versionBefore));
        }

        [Test]
        public void Close_is_idempotent()
        {
            var harness = new TerminalHarness();
            harness.RunLine("echo hi");

            harness.Session.Close();

            Assert.That(() => harness.Session.Close(), Throws.Nothing);
            Assert.That(harness.Session.IsClosed, Is.True);
        }

        [Test]
        public void Close_hangs_up_background_jobs_and_empties_the_job_table()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.RunLine("spin &");
            var spinPid = AnnouncedPid(harness.Session.ScrollbackText);

            harness.Session.Close();

            Assert.That(harness.Scheduler.GetExitCode(spinPid), Is.EqualTo(129));
            Assert.That(harness.Jobs.Jobs, Is.Empty);
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out _), Is.False);

            harness.Scheduler.Tick();
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Close_cascades_eof_so_all_foreground_members_exit()
        {
            var harness = new TerminalHarness();
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);
            const int executorPid = 1;
            const int catPid = 2;
            Assert.That(harness.Scheduler.Contains(executorPid), Is.True);
            Assert.That(harness.Scheduler.Contains(catPid), Is.True);

            harness.Session.Close();
            for (var tick = 0; tick < 8 && harness.Scheduler.ProcessCount > 0; tick++)
            {
                harness.Scheduler.Tick();
            }

            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
            Assert.That(harness.Scheduler.TryPeekExitCode(executorPid, out _), Is.False);
            Assert.That(harness.Scheduler.TryPeekExitCode(catPid, out _), Is.True);
        }

        [Test]
        public void Close_with_a_running_job_and_a_busy_foreground_command_drains_everything()
        {
            var harness = new TerminalHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.RunLine("spin &");
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);

            Assert.That(() => harness.Session.Close(), Throws.Nothing);
            for (var tick = 0; tick < 8 && harness.Scheduler.ProcessCount > 0; tick++)
            {
                harness.Scheduler.Tick();
            }

            Assert.That(harness.Jobs.Jobs, Is.Empty);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Pump_after_close_is_a_no_op()
        {
            var harness = new TerminalHarness();
            harness.RunLine("echo hi");
            harness.Session.Close();
            var versionAfterClose = harness.Session.ScrollbackVersion;

            Assert.That(() => harness.Session.Pump(), Throws.Nothing);
            Assert.That(harness.Session.ScrollbackVersion, Is.EqualTo(versionAfterClose));
        }

        [Test]
        public void Close_with_unflushed_pending_stdin_is_clean()
        {
            var harness = new TerminalHarness();
            harness.RunLine("cat", ticks: 2);
            Assert.That(harness.Session.IsBusy, Is.True);
            var bigLine = new string('a', 16000);
            for (var index = 0; index < 5; index++)
            {
                Assert.That(harness.Session.SubmitLine(bigLine), Is.True);
            }

            Assert.That(() => harness.Session.Close(), Throws.Nothing);
            for (var tick = 0; tick < 16 && harness.Scheduler.ProcessCount > 0; tick++)
            {
                harness.Scheduler.Tick();
            }

            Assert.That(harness.Session.IsClosed, Is.True);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Scrollback_stays_readable_after_close()
        {
            var harness = new TerminalHarness();
            harness.RunLine("echo hi");

            harness.Session.Close();

            Assert.That(harness.Session.ScrollbackText, Is.EqualTo("/ # echo hi\nhi\n"));
        }

        [Test]
        public void Null_line_throws()
        {
            var harness = new TerminalHarness();

            Assert.Throws<ArgumentNullException>(() => harness.Session.SubmitLine(null));
        }

        [Test]
        public void Submit_after_close_returns_false()
        {
            var harness = new TerminalHarness();
            harness.Session.Close();
            var versionBefore = harness.Session.ScrollbackVersion;

            Assert.That(harness.Session.SubmitLine("echo hi"), Is.False);
            Assert.That(harness.Session.ScrollbackVersion, Is.EqualTo(versionBefore));
        }

        [Test]
        public void Prompt_reflects_cwd_and_identity()
        {
            var rootHarness = new TerminalHarness();
            Assert.That(rootHarness.Session.PromptText, Is.EqualTo("/ # "));
            rootHarness.Vfs.CreateDirectory("/d", new Siegebox.Vfs.PermissionMode(0b111_101_101), new Siegebox.Vfs.Credentials(0));
            rootHarness.RunLine("cd /d");
            Assert.That(rootHarness.Session.PromptText, Is.EqualTo("/d # "));

            var userHarness = new TerminalHarness(uid: 1000);
            Assert.That(userHarness.Session.PromptText, Is.EqualTo("/ $ "));
        }
    }
}
