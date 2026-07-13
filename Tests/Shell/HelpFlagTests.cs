using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Process.Tests;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class HelpFlagTests
    {
        [Test]
        public void Cat_help_prints_the_synopsis_on_stdout()
        {
            var harness = new ShellHarness();

            harness.Run("cat --help");

            Assert.That(harness.DrainOutput(), Is.EqualTo("usage: cat [FILE]...\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Ls_help_prints_the_synopsis_on_stdout()
        {
            var harness = new ShellHarness();

            harness.Run("ls --help");

            Assert.That(harness.DrainOutput(), Is.EqualTo("usage: ls [FILE]...\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void An_unknown_flag_is_not_treated_as_help()
        {
            var harness = new ShellHarness();

            harness.Run("ls --nonsense");

            Assert.That(harness.DrainOutput(), Is.Empty);
            Assert.That(harness.DrainError(), Does.Contain("--nonsense"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void A_command_without_a_manual_entry_falls_through_to_normal_execution()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new NoteCommand());

            harness.Run("note --help");

            Assert.That(harness.DrainOutput(), Is.EqualTo("--help\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        private sealed class NoteCommand : ICommand
        {
            public string Name => "note";

            public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
            {
                var bytes = Encoding.UTF8.GetBytes(string.Join(" ", arguments) + "\n");
                return new ScriptedProcess(context, self =>
                {
                    context.FileDescriptors.Get(FileDescriptorTable.Stdout).Write(bytes, 0, bytes.Length);
                    self.ExitCode = 0;
                    return ProcessState.Finished;
                });
            }
        }
    }
}
