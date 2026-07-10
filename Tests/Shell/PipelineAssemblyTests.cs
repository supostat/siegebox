using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class PipelineAssemblyTests
    {
        private static byte[] Payload(int length)
        {
            var payload = new byte[length];
            for (var index = 0; index < length; index++)
            {
                payload[index] = (byte)(index % 251);
            }

            return payload;
        }

        [Test]
        public void Neighbour_stages_share_one_pipe_and_distinct_pairs_get_distinct_pipes()
        {
            var harness = new ShellHarness();
            var first = new ProbeCommand("p1");
            var second = new ProbeCommand("p2");
            var third = new ProbeCommand("p3");
            harness.RegisterCommand(first);
            harness.RegisterCommand(second);
            harness.RegisterCommand(third);

            harness.Run("p1 | p2 | p3");

            var firstOut = first.Contexts[0].FileDescriptors.Get(FileDescriptorTable.Stdout);
            var secondIn = second.Contexts[0].FileDescriptors.Get(FileDescriptorTable.Stdin);
            var secondOut = second.Contexts[0].FileDescriptors.Get(FileDescriptorTable.Stdout);
            var thirdIn = third.Contexts[0].FileDescriptors.Get(FileDescriptorTable.Stdin);
            Assert.That(firstOut, Is.SameAs(secondIn));
            Assert.That(secondOut, Is.SameAs(thirdIn));
            Assert.That(firstOut, Is.Not.SameAs(secondOut));
        }

        [Test]
        public void All_stages_share_the_same_terminal_error_stream()
        {
            var harness = new ShellHarness();
            var first = new ProbeCommand("p1");
            var second = new ProbeCommand("p2");
            harness.RegisterCommand(first);
            harness.RegisterCommand(second);

            harness.Run("p1 | p2");

            Assert.That(
                first.Contexts[0].FileDescriptors.Get(FileDescriptorTable.Stderr),
                Is.SameAs(second.Contexts[0].FileDescriptors.Get(FileDescriptorTable.Stderr)));
        }

        [Test]
        public void Context_snapshots_carry_the_session_cwd_credentials_and_environment()
        {
            var harness = new ShellHarness();
            var probe = new ProbeCommand("probe");
            harness.RegisterCommand(probe);
            harness.Vfs.CreateDirectory("/work", new PermissionMode(0b111_101_101), new Credentials(0));
            harness.Session.WorkingDirectory = "/work";
            harness.Session.Environment["GREETING"] = "hello";
            var credentials = new Credentials(7);
            harness.Session.Credentials = credentials;

            harness.Run("probe");

            var context = probe.Contexts[0];
            Assert.That(context.WorkingDirectory, Is.EqualTo("/work"));
            Assert.That(context.Credentials, Is.SameAs(credentials));
            Assert.That(context.Environment["GREETING"], Is.EqualTo("hello"));
        }

        [Test]
        public void Launched_stage_context_is_immune_to_later_session_mutation()
        {
            var harness = new ShellHarness();
            var probe = new ProbeCommand("probe");
            harness.RegisterCommand(probe);
            harness.Session.Environment["MODE"] = "before";

            harness.Run("probe");
            var context = probe.Contexts[0];

            harness.Session.WorkingDirectory = "/changed";
            harness.Session.Environment["MODE"] = "after";
            harness.Session.Credentials = new Credentials(1000);

            Assert.That(context.WorkingDirectory, Is.EqualTo("/"));
            Assert.That(context.Environment["MODE"], Is.EqualTo("before"));
            Assert.That(context.Credentials.Uid, Is.EqualTo(0));
        }

        [Test]
        public void Reader_attached_at_assembly_time_sees_a_full_first_step_payload()
        {
            var harness = new ShellHarness();
            var payload = Payload(16);
            var writer = new ByteWriterCommand("writeall", payload);
            var reader = new ByteReaderCommand("readall");
            harness.RegisterCommand(writer);
            harness.RegisterCommand(reader);

            harness.Run("writeall | readall");

            Assert.That(reader.LastProcess.Received, Is.EqualTo(payload));
            Assert.That(reader.LastProcess.SawEof, Is.True);
        }

        [Test]
        public void Redirect_displacing_a_pipe_end_keeps_the_cascade_alive()
        {
            var harness = new ShellHarness();
            var payload = Payload(16);
            var writer = new ByteWriterCommand("writeall", payload);
            var reader = new ByteReaderCommand("readall");
            harness.RegisterCommand(writer);
            harness.RegisterCommand(reader);

            harness.Run("writeall > /f | readall");

            Assert.That(reader.LastProcess.SawEof, Is.True);
            Assert.That(reader.LastProcess.Received, Is.Empty);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Middle_unknown_command_still_lets_the_other_stages_run_and_cascade()
        {
            var harness = new ShellHarness();
            var payload = Payload(16);
            var writer = new ByteWriterCommand("writeall", payload);
            var reader = new ByteReaderCommand("readall");
            harness.RegisterCommand(writer);
            harness.RegisterCommand(reader);

            harness.Run("writeall | nosuchcmd | readall");

            Assert.That(harness.DrainError(), Does.Contain("nosuchcmd: command not found"));
            Assert.That(reader.LastProcess.SawEof, Is.True);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }
    }
}
