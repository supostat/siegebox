using System;
using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class CommandRegistryTests
    {
        [Test]
        public void Registered_command_round_trips()
        {
            var registry = new CommandRegistry();
            var probe = new ProbeCommand("probe");

            registry.Register(probe);

            Assert.That(registry.TryGet("probe", out var found), Is.True);
            Assert.That(found, Is.SameAs(probe));
            Assert.That(registry.TryGet("absent", out _), Is.False);
        }

        [Test]
        public void Names_are_sorted_ordinal()
        {
            var registry = new CommandRegistry();
            registry.Register(new ProbeCommand("zeta"));
            registry.Register(new ProbeCommand("alpha"));
            registry.Register(new ProbeCommand("mike"));

            Assert.That(registry.Names, Is.EqualTo(new[] { "alpha", "mike", "zeta" }));
        }

        [Test]
        public void Duplicate_registration_throws()
        {
            var registry = new CommandRegistry();
            registry.Register(new ProbeCommand("twin"));

            Assert.Throws<ArgumentException>(() => registry.Register(new ProbeCommand("twin")));
        }

        [Test]
        public void Builtin_wins_over_a_command_with_the_same_name()
        {
            var harness = new ShellHarness();
            var impostor = new ProbeCommand("cd");
            harness.RegisterCommand(impostor);
            harness.Vfs.CreateDirectory("/dest", new PermissionMode(0b111_101_101), new Credentials(0));

            harness.Run("cd /dest");

            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/dest"));
            Assert.That(impostor.Contexts, Is.Empty);
        }

        [Test]
        public void Unknown_command_yields_127_and_or_if_recovers()
        {
            var harness = new ShellHarness();

            harness.Run("nosuchcmd || echo saved");

            Assert.That(harness.DrainError(), Does.Contain("nosuchcmd: command not found"));
            Assert.That(harness.DrainOutput(), Is.EqualTo("saved\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Double_install_of_the_base_command_set_collides_like_any_mod()
        {
            var commands = new CommandRegistry();
            var builtins = new BuiltinRegistry();
            var vfs = new VirtualFileSystem();
            var scheduler = new Scheduler();
            var jobs = new JobTable();
            BaseCommandSet.Install(commands, builtins, vfs, scheduler, jobs);

            Assert.Throws<ArgumentException>(() => BaseCommandSet.Install(commands, builtins, vfs, scheduler, jobs));
        }

        [Test]
        public void Install_builtins_rejects_null_arguments()
        {
            var builtins = new BuiltinRegistry();
            var vfs = new VirtualFileSystem();
            var scheduler = new Scheduler();
            var jobs = new JobTable();

            Assert.Throws<ArgumentNullException>(() => BaseCommandSet.InstallBuiltins(null, vfs, scheduler, jobs));
            Assert.Throws<ArgumentNullException>(() => BaseCommandSet.InstallBuiltins(builtins, null, scheduler, jobs));
            Assert.Throws<ArgumentNullException>(() => BaseCommandSet.InstallBuiltins(builtins, vfs, null, jobs));
            Assert.Throws<ArgumentNullException>(() => BaseCommandSet.InstallBuiltins(builtins, vfs, scheduler, null));
        }

        [Test]
        public void Command_registered_after_shell_construction_resolves_in_the_next_execute()
        {
            var harness = new ShellHarness();

            harness.Run("latecmd");
            Assert.That(harness.DrainError(), Does.Contain("latecmd: command not found"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(127));

            harness.RegisterCommand(new ProbeCommand("latecmd"));
            harness.Run("latecmd");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.DrainError(), Is.EqualTo(""));
        }
    }
}
