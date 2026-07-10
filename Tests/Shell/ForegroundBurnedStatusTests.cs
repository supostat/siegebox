using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Process.Tests;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class ForegroundBurnedStatusTests
    {
        private sealed class ShellRig
        {
            public ShellRig(int tickBudget)
            {
                Scheduler = new Scheduler(tickBudget);
                Vfs = new VirtualFileSystem();
                Commands = new CommandRegistry();
                Builtins = new BuiltinRegistry();
                Jobs = new JobTable();
                BaseCommandSet.Install(Commands, Builtins, Vfs, Scheduler, Jobs);
                Commands.Register(new SpinCommand());
                Session = new ShellSession("/", new Credentials(0));
                Shell = new Shell(
                    Scheduler, Vfs, Commands, Builtins, Session, Jobs,
                    new PipeStream(), new PipeStream(), new PipeStream());
            }

            public Scheduler Scheduler { get; }

            public VirtualFileSystem Vfs { get; }

            public CommandRegistry Commands { get; }

            public BuiltinRegistry Builtins { get; }

            public JobTable Jobs { get; }

            public ShellSession Session { get; }

            public Shell Shell { get; }

            public void Tick(int count)
            {
                for (var tick = 0; tick < count; tick++)
                {
                    Scheduler.Tick();
                }
            }
        }

        private static ExecutionContext CreateContext()
        {
            var descriptors = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
            return new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), descriptors);
        }

        [Test]
        public void Burned_status_synthesizes_127_and_the_shell_recovers()
        {
            var rig = new ShellRig(tickBudget: 8);
            var executorPid = rig.Shell.Execute("spin");
            rig.Tick(1);
            var spinPid = executorPid + 1;
            Assert.That(rig.Scheduler.Contains(spinPid), Is.True);

            rig.Scheduler.Kill(spinPid);
            Assert.That(rig.Scheduler.TryCollectExitCode(spinPid, out _), Is.True);

            rig.Tick(8);

            Assert.That(rig.Scheduler.Contains(executorPid), Is.False);
            Assert.That(rig.Session.LastExitCode, Is.EqualTo(127));

            rig.Shell.Execute("echo ok");
            rig.Tick(8);
            Assert.That(rig.Scheduler.ProcessCount, Is.EqualTo(0));
            Assert.That(rig.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Alive_foreground_member_still_waits()
        {
            var rig = new ShellRig(tickBudget: 8);
            var executorPid = rig.Shell.Execute("spin");
            var spinPid = executorPid + 1;

            rig.Tick(4);
            Assert.That(rig.Scheduler.Contains(executorPid), Is.True);
            Assert.That(rig.Scheduler.Contains(spinPid), Is.True);

            rig.Scheduler.Kill(spinPid);
            rig.Tick(4);

            Assert.That(rig.Scheduler.Contains(executorPid), Is.False);
            Assert.That(rig.Session.LastExitCode, Is.EqualTo(Scheduler.InterruptExitCode));
        }

        [Test]
        public void Same_tick_burn_window_resolves_to_127_without_spinning()
        {
            var rig = new ShellRig(tickBudget: 1);
            var executorPid = rig.Shell.Execute("spin");
            rig.Tick(1);
            var spinPid = executorPid + 1;
            Assert.That(rig.Scheduler.Contains(spinPid), Is.True);

            var burnedWhileCorpseInTable = false;
            var burglar = new ScriptedProcess(CreateContext(), self =>
            {
                rig.Scheduler.Kill(spinPid);
                rig.Scheduler.TryCollectExitCode(spinPid, out _);
                burnedWhileCorpseInTable = rig.Scheduler.Contains(spinPid);
                self.ExitCode = 0;
                return ProcessState.Finished;
            });
            rig.Scheduler.Spawn(burglar, "burglar");

            rig.Tick(32);

            Assert.That(burnedWhileCorpseInTable, Is.True);
            Assert.That(rig.Scheduler.Contains(executorPid), Is.False);
            Assert.That(rig.Session.LastExitCode, Is.EqualTo(127));
            Assert.That(rig.Scheduler.ProcessCount, Is.EqualTo(0));
        }
    }
}
