using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class ProcessLifecycleTests
    {
        private static readonly IReadOnlyList<string> NoArguments = Array.Empty<string>();

        private static ExecutionContext CreateContext(IByteStream stdin, IByteStream stdout)
        {
            var descriptors = new FileDescriptorTable(stdin, stdout, new PipeStream());
            return new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), descriptors);
        }

        private static ExecutionContext CreateContext()
        {
            return CreateContext(new PipeStream(), new PipeStream());
        }

        private static (int Pid, ScriptedProcess Process) SpawnScripted(
            Scheduler scheduler,
            ExecutionContext context,
            Func<ScriptedProcess, ProcessState> script)
        {
            ScriptedProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new ScriptedProcess(spawnContext, script);
                return created;
            });
            var pid = scheduler.Spawn(command, context, NoArguments);
            return (pid, created);
        }

        private static (int Pid, ScriptedProcess Process) SpawnScripted(
            Scheduler scheduler,
            Func<ScriptedProcess, ProcessState> script)
        {
            return SpawnScripted(scheduler, CreateContext(), script);
        }

        [Test]
        public void Budget_one_scheduler_steps_a_process_once_per_tick()
        {
            var scheduler = new Scheduler(1);
            var process = SpawnScripted(scheduler, self => ProcessState.Running).Process;

            scheduler.Tick();
            Assert.That(process.StepCount, Is.EqualTo(1));

            scheduler.Tick();
            Assert.That(process.StepCount, Is.EqualTo(2));
        }

        [Test]
        public void Sleeping_process_is_skipped_until_external_bytes_arrive()
        {
            var scheduler = new Scheduler(1);
            var stdin = new PipeStream();
            var sleeper = SpawnScripted(scheduler, CreateContext(stdin, new PipeStream()), self =>
            {
                self.WakeCondition = WakeCondition.Readable(stdin);
                return ProcessState.Sleeping;
            }).Process;

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));

            stdin.Write(new byte[] { 42 }, 0, 1);
            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(2));
        }

        [Test]
        public void Sleeping_without_a_wake_condition_throws_and_does_not_brick_the_scheduler()
        {
            var scheduler = new Scheduler(10);
            var process = SpawnScripted(scheduler, self =>
            {
                if (self.StepCount == 1)
                {
                    return ProcessState.Sleeping;
                }

                self.ExitCode = 0;
                return ProcessState.Finished;
            }).Process;

            Assert.Throws<InvalidOperationException>(() => scheduler.Tick());

            Assert.That(() => scheduler.Tick(), Throws.Nothing);
            Assert.That(process.StepCount, Is.EqualTo(2));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Wait_takes_no_steps_while_the_target_lives_and_wakes_in_the_tick_it_finishes()
        {
            var scheduler = new Scheduler();
            var worker = SpawnScripted(scheduler, self =>
            {
                if (self.StepCount < 3)
                {
                    return ProcessState.Running;
                }

                self.ExitCode = 0;
                return ProcessState.Finished;
            });
            var waiter = SpawnScripted(scheduler, self =>
            {
                if (self.StepCount == 1)
                {
                    self.WakeCondition = WakeCondition.ProcessExit(worker.Pid);
                    return ProcessState.Sleeping;
                }

                self.ExitCode = 0;
                return ProcessState.Finished;
            });

            scheduler.Tick();

            Assert.That(worker.Process.StepCount, Is.EqualTo(3));
            Assert.That(waiter.Process.StepCount, Is.EqualTo(2));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Wait_on_a_never_existed_pid_wakes_immediately()
        {
            var scheduler = new Scheduler();
            var waiter = SpawnScripted(scheduler, self =>
            {
                if (self.StepCount == 1)
                {
                    self.WakeCondition = WakeCondition.ProcessExit(999);
                    return ProcessState.Sleeping;
                }

                self.ExitCode = 0;
                return ProcessState.Finished;
            }).Process;

            scheduler.Tick();

            Assert.That(waiter.StepCount, Is.EqualTo(2));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Wait_on_a_reaped_pid_wakes_immediately()
        {
            var scheduler = new Scheduler();
            var (workerPid, _) = SpawnScripted(scheduler, self =>
            {
                self.ExitCode = 0;
                return ProcessState.Finished;
            });
            scheduler.Tick();
            Assert.That(scheduler.Contains(workerPid), Is.False);

            var waiter = SpawnScripted(scheduler, self =>
            {
                if (self.StepCount == 1)
                {
                    self.WakeCondition = WakeCondition.ProcessExit(workerPid);
                    return ProcessState.Sleeping;
                }

                self.ExitCode = 0;
                return ProcessState.Finished;
            }).Process;

            scheduler.Tick();

            Assert.That(waiter.StepCount, Is.EqualTo(2));
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Finished_process_is_never_resurrected_by_its_reported_state()
        {
            var scheduler = new Scheduler(10);
            var pid = 0;
            ScriptedProcess process = null;
            var observedState = ProcessState.Running;
            var observedExitCode = 0;
            (pid, process) = SpawnScripted(scheduler, self =>
            {
                scheduler.Kill(pid);
                observedState = scheduler.GetState(pid);
                observedExitCode = scheduler.GetExitCode(pid);
                return ProcessState.Running;
            });

            scheduler.Tick();

            Assert.That(process.StepCount, Is.EqualTo(1));
            Assert.That(observedState, Is.EqualTo(ProcessState.Finished));
            Assert.That(observedExitCode, Is.EqualTo(Scheduler.InterruptExitCode));
            Assert.That(scheduler.Contains(pid), Is.False);
        }

        [Test]
        public void Natural_finish_closes_descriptors_and_cascades_eof()
        {
            var scheduler = new Scheduler();
            var stdout = new PipeStream();
            SpawnScripted(scheduler, CreateContext(new PipeStream(), stdout), self =>
            {
                self.Context.FileDescriptors.Get(FileDescriptorTable.Stdout).Write(new byte[] { 5, 6 }, 0, 2);
                self.ExitCode = 0;
                return ProcessState.Finished;
            });

            scheduler.Tick();

            var drained = new byte[8];
            Assert.That(stdout.Read(drained, 0, 8).Count, Is.EqualTo(2));
            Assert.That(stdout.Read(drained, 0, 8).Status, Is.EqualTo(StreamStatus.Eof));
        }
    }
}
