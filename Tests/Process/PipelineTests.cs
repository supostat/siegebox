using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class PipelineTests
    {
        private static readonly IReadOnlyList<string> NoArguments = Array.Empty<string>();

        private static ExecutionContext CreateContext(IByteStream stdin, IByteStream stdout)
        {
            var descriptors = new FileDescriptorTable(stdin, stdout, new PipeStream());
            return new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), descriptors);
        }

        private static (int Pid, WriterProcess Process) SpawnWriter(
            Scheduler scheduler,
            IByteStream stdout,
            byte[] payload,
            int chunkSize)
        {
            WriterProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new WriterProcess(spawnContext, payload, chunkSize);
                return created;
            });
            var pid = scheduler.Spawn(command, CreateContext(new PipeStream(), stdout), NoArguments);
            return (pid, created);
        }

        private static (int Pid, ReaderProcess Process) SpawnReader(Scheduler scheduler, IByteStream stdin, int chunkSize)
        {
            ReaderProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new ReaderProcess(spawnContext, chunkSize);
                return created;
            });
            var pid = scheduler.Spawn(command, CreateContext(stdin, new PipeStream()), NoArguments);
            return (pid, created);
        }

        private static (int Pid, RelayProcess Process) SpawnRelay(
            Scheduler scheduler,
            IByteStream stdin,
            IByteStream stdout,
            int chunkSize)
        {
            RelayProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new RelayProcess(spawnContext, chunkSize);
                return created;
            });
            var pid = scheduler.Spawn(command, CreateContext(stdin, stdout), NoArguments);
            return (pid, created);
        }

        private static (int Pid, ScriptedProcess Process) SpawnInfinite(Scheduler scheduler, ExecutionContext context)
        {
            ScriptedProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new ScriptedProcess(spawnContext, self => ProcessState.Running);
                return created;
            });
            var pid = scheduler.Spawn(command, context, NoArguments);
            return (pid, created);
        }

        private static void RunUntilIdle(Scheduler scheduler, int maxTicks = 64)
        {
            for (var tick = 0; tick < maxTicks && scheduler.ProcessCount > 0; tick++)
            {
                scheduler.Tick();
            }
        }

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
        public void Pipeline_transfers_all_bytes_and_collapses()
        {
            var scheduler = new Scheduler();
            var pipe = new PipeStream();
            var payload = Payload(64);
            var writer = SpawnWriter(scheduler, pipe, payload, chunkSize: 8).Process;
            var reader = SpawnReader(scheduler, pipe, chunkSize: 8).Process;

            RunUntilIdle(scheduler);

            Assert.That(reader.Received, Is.EqualTo(payload));
            Assert.That(reader.SawEof, Is.True);
            Assert.That(writer.ExitCode, Is.EqualTo(0));
            Assert.That(reader.ExitCode, Is.EqualTo(0));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Small_pipeline_completes_within_a_single_tick()
        {
            var scheduler = new Scheduler();
            var pipe = new PipeStream();
            var payload = Payload(32);
            SpawnWriter(scheduler, pipe, payload, chunkSize: 8);
            var reader = SpawnReader(scheduler, pipe, chunkSize: 8).Process;

            scheduler.Tick();

            Assert.That(reader.Received, Is.EqualTo(payload));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Reader_attached_before_the_first_tick_sees_the_whole_payload()
        {
            var scheduler = new Scheduler();
            var pipe = new PipeStream();
            var payload = Payload(16);
            SpawnWriter(scheduler, pipe, payload, chunkSize: payload.Length);
            var reader = SpawnReader(scheduler, pipe, chunkSize: 4).Process;

            RunUntilIdle(scheduler);

            Assert.That(reader.Received, Is.EqualTo(payload));
            Assert.That(reader.SawEof, Is.True);
        }

        [Test]
        public void Backpressure_blocks_the_writer_and_preserves_byte_order()
        {
            var scheduler = new Scheduler();
            var pipe = new PipeStream(capacity: 4);
            var payload = Payload(32);
            var writer = SpawnWriter(scheduler, pipe, payload, chunkSize: 8).Process;

            scheduler.Tick();
            Assert.That(writer.BlockedCount, Is.GreaterThan(0));

            var reader = SpawnReader(scheduler, pipe, chunkSize: 8).Process;
            RunUntilIdle(scheduler);

            Assert.That(reader.Received, Is.EqualTo(payload));
            Assert.That(reader.SawEof, Is.True);
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Three_stage_pipeline_relays_the_payload_and_collapses_transitively()
        {
            var scheduler = new Scheduler();
            var upstream = new PipeStream();
            var downstream = new PipeStream();
            var payload = Payload(48);
            var writer = SpawnWriter(scheduler, upstream, payload, chunkSize: 8).Process;
            var relay = SpawnRelay(scheduler, upstream, downstream, chunkSize: 8).Process;
            var reader = SpawnReader(scheduler, downstream, chunkSize: 8).Process;

            RunUntilIdle(scheduler);

            Assert.That(reader.Received, Is.EqualTo(payload));
            Assert.That(reader.SawEof, Is.True);
            Assert.That(writer.ExitCode, Is.EqualTo(0));
            Assert.That(relay.ExitCode, Is.EqualTo(0));
            Assert.That(reader.ExitCode, Is.EqualTo(0));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Background_process_survives_the_pipeline_collapse()
        {
            var scheduler = new Scheduler();
            var pipe = new PipeStream();
            var payload = Payload(16);
            SpawnWriter(scheduler, pipe, payload, chunkSize: 8);
            SpawnReader(scheduler, pipe, chunkSize: 8);
            var background = SpawnInfinite(scheduler, CreateContext(new PipeStream(), new PipeStream()));

            scheduler.Tick();

            Assert.That(scheduler.ProcessCount, Is.EqualTo(1));
            Assert.That(scheduler.Contains(background.Pid), Is.True);
            var stepsAfterCollapse = background.Process.StepCount;

            scheduler.Tick();
            Assert.That(background.Process.StepCount, Is.GreaterThan(stepsAfterCollapse));
        }

        [Test]
        public void Killed_writer_wakes_the_sleeping_reader_with_eof()
        {
            var scheduler = new Scheduler(16);
            var pipe = new PipeStream();
            var writerPid = SpawnInfinite(scheduler, CreateContext(new PipeStream(), pipe)).Pid;
            var (readerPid, reader) = SpawnReader(scheduler, pipe, chunkSize: 8);

            scheduler.Tick();
            Assert.That(scheduler.GetState(readerPid), Is.EqualTo(ProcessState.Sleeping));
            Assert.That(reader.SawEof, Is.False);

            scheduler.Kill(writerPid);
            scheduler.Tick();

            Assert.That(reader.SawEof, Is.True);
            Assert.That(reader.ExitCode, Is.EqualTo(0));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Killed_reader_unblocks_the_writer_with_broken_pipe()
        {
            var scheduler = new Scheduler(16);
            var pipe = new PipeStream(capacity: 2);
            var (writerPid, writer) = SpawnWriter(scheduler, pipe, Payload(8), chunkSize: 4);
            var readerPid = SpawnInfinite(scheduler, CreateContext(pipe, new PipeStream())).Pid;

            scheduler.Tick();
            Assert.That(scheduler.GetState(writerPid), Is.EqualTo(ProcessState.Sleeping));
            Assert.That(writer.BlockedCount, Is.GreaterThan(0));

            scheduler.Kill(readerPid);
            scheduler.Tick();

            Assert.That(writer.SawBrokenPipe, Is.True);
            Assert.That(writer.ExitCode, Is.EqualTo(WriterProcess.BrokenPipeExitCode));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }
    }
}
