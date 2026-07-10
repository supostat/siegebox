using System;
using NUnit.Framework;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class ShellExecutionTests
    {
        [Test]
        public void Pipeline_with_redirect_and_and_if_runs_end_to_end()
        {
            var harness = new ShellHarness();

            harness.Run("echo hi | cat > /out && echo ok");

            Assert.That(harness.ReadFile("/out"), Is.EqualTo("hi\n"));
            Assert.That(harness.DrainOutput(), Is.EqualTo("ok\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Or_if_recovers_from_a_failed_command()
        {
            var harness = new ShellHarness();

            harness.Run("cat /missing || echo rescued");

            Assert.That(harness.DrainError(), Does.Contain("cat: /missing: No such file or directory"));
            Assert.That(harness.DrainOutput(), Is.EqualTo("rescued\n"));
        }

        [Test]
        public void And_if_after_a_failure_is_skipped_and_keeps_the_exit_code()
        {
            var harness = new ShellHarness();

            harness.Run("cat /missing && echo never");

            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Semicolon_runs_both_pipelines()
        {
            var harness = new ShellHarness();

            harness.Run("echo one ; echo two");

            Assert.That(harness.DrainOutput(), Is.EqualTo("one\ntwo\n"));
        }

        [Test]
        public void Middle_stage_failure_leaves_the_last_stages_exit_code()
        {
            var harness = new ShellHarness();

            harness.Run("cat /missing | echo ok && echo ran");

            Assert.That(harness.DrainOutput(), Is.EqualTo("ok\nran\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Semicolon_runs_the_second_pipeline_after_a_failure()
        {
            var harness = new ShellHarness();

            harness.Run("cat /missing ; echo still");

            Assert.That(harness.DrainOutput(), Is.EqualTo("still\n"));
        }

        [Test]
        public void Or_if_after_a_success_is_skipped()
        {
            var harness = new ShellHarness();

            harness.Run("echo ok || echo never");

            Assert.That(harness.DrainOutput(), Is.EqualTo("ok\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Skipped_and_if_leaves_the_exit_code_for_the_following_or_if()
        {
            var harness = new ShellHarness();

            harness.Run("cat /missing && echo never || echo rescued");

            Assert.That(harness.DrainOutput(), Is.EqualTo("rescued\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Parse_error_sets_exit_code_two_synchronously_before_any_tick()
        {
            var harness = new ShellHarness();

            harness.Shell.Execute("echo hi |");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(2));

            harness.RunUntilIdle();
            Assert.That(harness.DrainError(), Does.StartWith("sh: "));
        }

        [Test]
        public void Consecutive_executes_both_reach_the_terminal()
        {
            var harness = new ShellHarness();

            harness.Run("echo first");
            Assert.That(harness.DrainOutput(), Is.EqualTo("first\n"));

            harness.Run("echo second");
            Assert.That(harness.DrainOutput(), Is.EqualTo("second\n"));
        }

        [Test]
        public void Blank_line_runs_nothing_and_leaves_the_exit_code_untouched()
        {
            var harness = new ShellHarness();
            harness.Session.LastExitCode = 5;

            var pid = harness.Shell.Execute("   ");

            Assert.That(pid, Is.EqualTo(0));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(5));
        }

        [Test]
        public void Parse_error_sets_exit_code_two_and_runs_nothing()
        {
            var harness = new ShellHarness();

            harness.Run("echo hi |");

            Assert.That(harness.DrainError(), Does.StartWith("sh: "));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(2));
        }

        [Test]
        public void Concurrent_execute_throws()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.Shell.Execute("spin");

            Assert.Throws<InvalidOperationException>(() => harness.Shell.Execute("echo x"));
        }

        [Test]
        public void Null_line_is_rejected()
        {
            var harness = new ShellHarness();

            Assert.Throws<ArgumentNullException>(() => harness.Shell.Execute(null));
        }

        [Test]
        public void Quoted_empty_command_is_command_not_found()
        {
            var harness = new ShellHarness();

            Assert.That(() => harness.Run("\"\""), Throws.Nothing);

            Assert.That(harness.DrainError(), Does.Contain("command not found"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Quoted_empty_stage_inside_a_pipeline_does_not_crash()
        {
            var harness = new ShellHarness();

            Assert.That(() => harness.Run("echo a | '' | cat"), Throws.Nothing);

            Assert.That(harness.DrainError(), Does.Contain("command not found"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Unknown_command_alone_sets_exit_code_127()
        {
            var harness = new ShellHarness();

            harness.Run("nosuchcmd");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));
        }

        [Test]
        public void Foreground_pipeline_statuses_are_fully_collected()
        {
            var harness = new ShellHarness();

            var executorPid = harness.Run("echo a | cat");

            Assert.That(harness.Scheduler.TryPeekExitCode(executorPid + 1, out _), Is.False);
            Assert.That(harness.Scheduler.TryPeekExitCode(executorPid + 2, out _), Is.False);
            Assert.That(harness.Scheduler.TryPeekExitCode(executorPid, out _), Is.True);

            harness.Run("echo b");
            Assert.That(harness.Scheduler.TryPeekExitCode(executorPid, out _), Is.False);
        }
    }
}
