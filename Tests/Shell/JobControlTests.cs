using NUnit.Framework;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class JobControlTests
    {
        private static ShellHarness CreateHarnessWithSpin()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());
            return harness;
        }

        private static int AnnouncedPid(string announcement) => ShellHarness.AnnouncedPid(announcement);

        [Test]
        public void Background_job_is_announced_and_outlives_the_executor()
        {
            var harness = CreateHarnessWithSpin();
            harness.Session.LastExitCode = 5;

            harness.Run("spin &", maxTicks: 8);

            var announcement = harness.DrainError();
            Assert.That(announcement, Does.Match(@"^\[1\] \d+\n$"));
            var spinPid = AnnouncedPid(announcement);
            Assert.That(harness.Scheduler.Contains(spinPid), Is.True);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(1));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Jobs_lists_a_running_job()
        {
            var harness = CreateHarnessWithSpin();
            harness.Run("spin &", maxTicks: 8);
            var spinPid = AnnouncedPid(harness.DrainError());

            harness.Run("jobs", maxTicks: 8);

            Assert.That(harness.DrainOutput(), Is.EqualTo($"[1] {spinPid} Running spin\n"));
            Assert.That(harness.Jobs.Jobs.Count, Is.EqualTo(1));
        }

        [Test]
        public void Multi_stage_background_pipeline_announces_the_last_stages_pid()
        {
            var harness = CreateHarnessWithSpin();

            harness.Run("echo hi | spin &", maxTicks: 8);

            var announcedPid = AnnouncedPid(harness.DrainError());
            var job = harness.Jobs.Jobs[0];
            Assert.That(job.Pids.Count, Is.EqualTo(2));
            Assert.That(announcedPid, Is.EqualTo(job.LastPid));
            Assert.That(announcedPid, Is.EqualTo(job.Pids[1]));
            Assert.That(announcedPid, Is.GreaterThan(job.Pids[0]));
        }

        [Test]
        public void Second_background_job_gets_number_two()
        {
            var harness = CreateHarnessWithSpin();

            harness.Run("spin &", maxTicks: 8);
            Assert.That(harness.DrainError(), Does.StartWith("[1] "));

            harness.Run("spin &", maxTicks: 8);
            Assert.That(harness.DrainError(), Does.StartWith("[2] "));
        }

        [Test]
        public void Kill_and_wait_empty_the_job_table()
        {
            var harness = CreateHarnessWithSpin();
            harness.Run("spin &", maxTicks: 8);
            var spinPid = AnnouncedPid(harness.DrainError());

            harness.Run($"kill {spinPid}");
            harness.Run("wait");

            Assert.That(harness.Jobs.Jobs, Is.Empty);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Wait_blocks_until_the_awaited_job_is_killed()
        {
            var harness = CreateHarnessWithSpin();
            harness.Run("spin &", maxTicks: 8);
            var spinPid = AnnouncedPid(harness.DrainError());

            var executorPid = harness.Shell.Execute("wait");
            harness.Tick(4);
            Assert.That(harness.Scheduler.Contains(executorPid), Is.True);

            harness.Scheduler.Kill(spinPid);
            harness.RunUntilIdle();

            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
            Assert.That(harness.Jobs.Jobs, Is.Empty);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Wait_on_an_unknown_pid_returns_127()
        {
            var harness = new ShellHarness();

            harness.Run("wait 9999");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));
        }

        [Test]
        public void Wait_on_a_non_numeric_pid_returns_127()
        {
            var harness = new ShellHarness();

            harness.Run("wait notapid");

            Assert.That(harness.DrainError(), Does.Contain("wait: notapid"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));
        }

        [Test]
        public void Wait_on_a_finished_background_pid_returns_its_exit_code()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new ProbeCommand("exit3", 3));
            harness.Run("exit3 &", maxTicks: 8);
            var probePid = AnnouncedPid(harness.DrainError());

            harness.Run($"wait {probePid}");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(3));
        }

        [Test]
        public void Jobs_reports_a_killed_job_as_done_once_and_collects_it()
        {
            var harness = CreateHarnessWithSpin();
            harness.Run("spin &", maxTicks: 8);
            var spinPid = AnnouncedPid(harness.DrainError());
            harness.Run($"kill {spinPid}");

            harness.Run("jobs");

            Assert.That(harness.DrainOutput(), Is.EqualTo($"[1] {spinPid} Done spin\n"));
            Assert.That(harness.Jobs.Jobs, Is.Empty);
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out _), Is.False);
        }

        [Test]
        public void Jobs_with_an_empty_table_prints_nothing_and_succeeds()
        {
            var harness = new ShellHarness();

            harness.Run("jobs");

            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Wait_on_multiple_pids_returns_the_last_pids_code()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new ProbeCommand("exit3", 3));
            harness.RegisterCommand(new ProbeCommand("exit5", 5));
            harness.Run("exit3 &", maxTicks: 8);
            var firstPid = AnnouncedPid(harness.DrainError());
            harness.Run("exit5 &", maxTicks: 8);
            var secondPid = AnnouncedPid(harness.DrainError());

            harness.Run($"wait {firstPid} {secondPid}");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(5));
        }
    }
}
