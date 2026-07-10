using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Process.Tests;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class WaitSessionScopeTests
    {
        private static int SpawnForeignSpin(ShellHarness harness)
        {
            var descriptors = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
            var context = new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), descriptors);
            return harness.Scheduler.Spawn(new ScriptedProcess(context, self => ProcessState.Running), "foreign");
        }

        [Test]
        public void Wait_rejects_a_live_pid_that_is_not_a_child_of_this_shell()
        {
            var harness = new ShellHarness();
            var foreignPid = SpawnForeignSpin(harness);

            harness.Run($"wait {foreignPid}");

            Assert.That(harness.DrainError(), Does.Contain($"wait: pid {foreignPid} is not a child of this shell"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));
            Assert.That(harness.Scheduler.Contains(foreignPid), Is.True);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(1));
        }

        [Test]
        public void Wait_rejects_the_executors_own_pid_instead_of_deadlocking()
        {
            var harness = new ShellHarness();
            var warmupExecutorPid = harness.Run("echo warmup");
            harness.DrainOutput();
            var nextExecutorPid = warmupExecutorPid + 2;

            harness.Run($"wait {nextExecutorPid}");

            Assert.That(harness.DrainError(), Does.Contain($"wait: pid {nextExecutorPid} is not a child of this shell"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Wait_still_collects_a_pid_belonging_to_this_shells_job()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());
            harness.Run($"kill {spinPid}");

            harness.Run($"wait {spinPid}");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(Scheduler.InterruptExitCode));
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out _), Is.False);
        }
    }
}
