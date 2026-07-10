using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class SchedulerHangupTests
    {
        private static ExecutionContext CreateContext(IByteStream stdin, IByteStream stdout)
        {
            var descriptors = new FileDescriptorTable(stdin, stdout, new PipeStream());
            return new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), descriptors);
        }

        private static (int Pid, ScriptedProcess Process) SpawnInfinite(Scheduler scheduler, ExecutionContext context)
        {
            var process = new ScriptedProcess(context, self => ProcessState.Running);
            return (scheduler.Spawn(process, "spin"), process);
        }

        [Test]
        public void Hangup_finishes_a_running_process_with_129()
        {
            var scheduler = new Scheduler();
            var pid = SpawnInfinite(scheduler, CreateContext(new PipeStream(), new PipeStream())).Pid;

            scheduler.Hangup(pid);

            Assert.That(Scheduler.HangupExitCode, Is.EqualTo(129));
            Assert.That(scheduler.GetState(pid), Is.EqualTo(ProcessState.Finished));
            Assert.That(scheduler.GetExitCode(pid), Is.EqualTo(129));
        }

        [Test]
        public void Hangup_cascades_eof_to_a_piped_reader()
        {
            var scheduler = new Scheduler(16);
            var pipe = new PipeStream();
            var writerPid = SpawnInfinite(scheduler, CreateContext(new PipeStream(), pipe)).Pid;
            var reader = new ReaderProcess(CreateContext(pipe, new PipeStream()), 8);
            scheduler.Spawn(reader, "reader");

            scheduler.Tick();
            Assert.That(reader.SawEof, Is.False);

            scheduler.Hangup(writerPid);
            scheduler.Tick();

            Assert.That(reader.SawEof, Is.True);
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Hangup_on_a_corpse_preserves_the_first_exit_code()
        {
            var scheduler = new Scheduler();
            var pid = SpawnInfinite(scheduler, CreateContext(new PipeStream(), new PipeStream())).Pid;
            scheduler.Kill(pid);

            Assert.That(() => scheduler.Hangup(pid), Throws.Nothing);
            Assert.That(scheduler.GetExitCode(pid), Is.EqualTo(Scheduler.InterruptExitCode));

            var second = SpawnInfinite(scheduler, CreateContext(new PipeStream(), new PipeStream())).Pid;
            scheduler.Hangup(second);
            scheduler.Kill(second);
            Assert.That(scheduler.GetExitCode(second), Is.EqualTo(Scheduler.HangupExitCode));
        }

        [Test]
        public void Hangup_retains_the_status_exactly_once()
        {
            var scheduler = new Scheduler();
            var pid = SpawnInfinite(scheduler, CreateContext(new PipeStream(), new PipeStream())).Pid;

            scheduler.Hangup(pid);
            scheduler.Tick();

            Assert.That(scheduler.Contains(pid), Is.False);
            Assert.That(scheduler.TryCollectExitCode(pid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(129));
            Assert.That(scheduler.TryPeekExitCode(pid, out _), Is.False);
        }

        [Test]
        public void Hangup_on_an_unknown_pid_throws()
        {
            var scheduler = new Scheduler();

            Assert.Throws<ArgumentException>(() => scheduler.Hangup(999));
        }
    }
}
