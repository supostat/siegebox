using System.Text.RegularExpressions;
using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Shell.Tests;
using Siegebox.Terminal;
using Siegebox.Vfs;

namespace Siegebox.Terminal.Tests
{
    [TestFixture]
    public sealed class TwoTerminalSessionsTests
    {
        private sealed class TwoSessionRig
        {
            public TwoSessionRig()
            {
                Vfs = new VirtualFileSystem();
                Scheduler = new Scheduler();
                Commands = new CommandRegistry();
                var builtinsA = new BuiltinRegistry();
                JobsA = new JobTable();
                BaseCommandSet.Install(Commands, builtinsA, Vfs, Scheduler, JobsA);
                Commands.Register(new SpinCommand());

                var builtinsB = new BuiltinRegistry();
                JobsB = new JobTable();
                BaseCommandSet.InstallBuiltins(builtinsB, Vfs, Scheduler, JobsB);

                SessionA = new ShellSession("/", new Credentials(0));
                SessionB = new ShellSession("/", new Credentials(0));
                TerminalA = new TerminalSession(Scheduler, Vfs, Commands, builtinsA, SessionA, JobsA);
                TerminalB = new TerminalSession(Scheduler, Vfs, Commands, builtinsB, SessionB, JobsB);
            }

            public VirtualFileSystem Vfs { get; }

            public Scheduler Scheduler { get; }

            public CommandRegistry Commands { get; }

            public JobTable JobsA { get; }

            public JobTable JobsB { get; }

            public ShellSession SessionA { get; }

            public ShellSession SessionB { get; }

            public TerminalSession TerminalA { get; }

            public TerminalSession TerminalB { get; }

            public void TickAndPumpAll(int count = 8)
            {
                for (var tick = 0; tick < count; tick++)
                {
                    Scheduler.Tick();
                    TerminalA.Pump();
                    TerminalB.Pump();
                }
            }
        }

        [Test]
        public void Closing_one_session_leaves_the_other_command_running()
        {
            var rig = new TwoSessionRig();
            rig.TerminalA.SubmitLine("cat");
            rig.TerminalB.SubmitLine("cat");
            rig.TickAndPumpAll(2);
            Assert.That(rig.TerminalA.IsBusy, Is.True);
            Assert.That(rig.TerminalB.IsBusy, Is.True);

            rig.TerminalB.Close();
            rig.TickAndPumpAll(4);

            Assert.That(rig.TerminalA.IsBusy, Is.True);
            rig.TerminalA.SubmitLine("still alive");
            rig.TickAndPumpAll(4);
            Assert.That(rig.TerminalA.ScrollbackText, Does.Contain("still alive\nstill alive\n"));
        }

        [Test]
        public void Scrollbacks_are_isolated()
        {
            var rig = new TwoSessionRig();

            rig.TerminalA.SubmitLine("echo alpha");
            rig.TerminalB.SubmitLine("echo beta");
            rig.TickAndPumpAll();

            Assert.That(rig.TerminalA.ScrollbackText, Does.Contain("alpha\n"));
            Assert.That(rig.TerminalA.ScrollbackText, Does.Not.Contain("beta"));
            Assert.That(rig.TerminalB.ScrollbackText, Does.Contain("beta\n"));
            Assert.That(rig.TerminalB.ScrollbackText, Does.Not.Contain("alpha"));
        }

        [Test]
        public void Cross_session_wait_returns_127()
        {
            var rig = new TwoSessionRig();
            rig.TerminalA.SubmitLine("spin &");
            rig.TickAndPumpAll(4);
            var spinPid = int.Parse(Regex.Match(rig.TerminalA.ScrollbackText, @"\[1\] (\d+)").Groups[1].Value);

            rig.TerminalB.SubmitLine($"wait {spinPid}");
            rig.TickAndPumpAll(8);

            Assert.That(rig.TerminalB.ScrollbackText, Does.Contain($"wait: pid {spinPid} is not a child of this shell"));
            Assert.That(rig.SessionB.LastExitCode, Is.EqualTo(127));
            Assert.That(rig.Scheduler.Contains(spinPid), Is.True);
        }
    }
}
