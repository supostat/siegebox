using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class SchedulerTests
    {
        private static readonly IReadOnlyList<string> NoArguments = Array.Empty<string>();

        private static ExecutionContext CreateContext()
        {
            var descriptors = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
            return new ExecutionContext("/", new Credentials(0), new Dictionary<string, string>(), descriptors);
        }

        private static ProcessState RunForever(ScriptedProcess process) => ProcessState.Running;

        private static (int Pid, ScriptedProcess Process) SpawnScripted(
            Scheduler scheduler,
            Func<ScriptedProcess, ProcessState> script)
        {
            ScriptedProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new ScriptedProcess(spawnContext, script);
                return created;
            });
            var pid = scheduler.Spawn(command, CreateContext(), NoArguments);
            return (pid, created);
        }

        [Test]
        public void Spawn_assigns_distinct_increasing_pids()
        {
            var scheduler = new Scheduler();

            var first = SpawnScripted(scheduler, RunForever).Pid;
            var second = SpawnScripted(scheduler, RunForever).Pid;
            var third = SpawnScripted(scheduler, RunForever).Pid;

            Assert.That(second, Is.GreaterThan(first));
            Assert.That(third, Is.GreaterThan(second));
        }

        [Test]
        public void Contains_and_process_count_track_spawned_processes()
        {
            var scheduler = new Scheduler();
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));

            var pid = SpawnScripted(scheduler, RunForever).Pid;

            Assert.That(scheduler.Contains(pid), Is.True);
            Assert.That(scheduler.Contains(pid + 1), Is.False);
            Assert.That(scheduler.ProcessCount, Is.EqualTo(1));
        }

        [Test]
        public void Spawn_passes_the_exact_context_and_arguments_to_the_command()
        {
            var scheduler = new Scheduler();
            var context = CreateContext();
            var arguments = new[] { "alpha", "beta" };
            ScriptedProcess created = null;
            var command = new StubCommand((spawnContext, spawnArguments) =>
            {
                created = new ScriptedProcess(spawnContext, RunForever);
                return created;
            });

            scheduler.Spawn(command, context, arguments);

            Assert.That(command.LastContext, Is.SameAs(context));
            Assert.That(command.LastArguments, Is.SameAs(arguments));
            Assert.That(command.LastCreated, Is.SameAs(created));
            Assert.That(created.Context, Is.SameAs(context));
        }

        [Test]
        public void Spawn_never_steps_the_new_process()
        {
            var scheduler = new Scheduler();

            var process = SpawnScripted(scheduler, RunForever).Process;

            Assert.That(process.StepCount, Is.EqualTo(0));
        }

        [Test]
        public void Tick_consumes_exactly_the_budget_on_an_infinite_process()
        {
            var scheduler = new Scheduler(10);
            var process = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();
            Assert.That(process.StepCount, Is.EqualTo(10));

            scheduler.Tick();
            Assert.That(process.StepCount, Is.EqualTo(20));
        }

        [Test]
        public void Tick_splits_the_budget_fairly_between_two_infinite_processes()
        {
            var scheduler = new Scheduler(10);
            var first = SpawnScripted(scheduler, RunForever).Process;
            var second = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();

            Assert.That(first.StepCount, Is.EqualTo(5));
            Assert.That(second.StepCount, Is.EqualTo(5));
        }

        [Test]
        public void Cursor_persists_across_ticks_and_keeps_scheduling_fair()
        {
            var scheduler = new Scheduler(3);
            var first = SpawnScripted(scheduler, RunForever).Process;
            var second = SpawnScripted(scheduler, RunForever).Process;

            for (var tick = 1; tick <= 5; tick++)
            {
                scheduler.Tick();
                Assert.That(Math.Abs(first.StepCount - second.StepCount), Is.LessThanOrEqualTo(1));
                Assert.That(first.StepCount + second.StepCount, Is.EqualTo(tick * 3));
            }
        }

        [Test]
        public void Unsatisfied_sleeper_consumes_no_budget()
        {
            var scheduler = new Scheduler(10);
            var emptyPipe = new PipeStream();
            var sleeper = SpawnScripted(scheduler, self =>
            {
                self.WakeCondition = WakeCondition.Readable(emptyPipe);
                return ProcessState.Sleeping;
            }).Process;
            var runner = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(runner.StepCount, Is.EqualTo(9));

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(runner.StepCount, Is.EqualTo(19));
        }

        [Test]
        public void Unsatisfied_writable_sleeper_consumes_no_budget()
        {
            var scheduler = new Scheduler(10);
            var fullPipe = new PipeStream(capacity: 1);
            fullPipe.Write(new byte[] { 42 }, 0, 1);
            var sleeper = SpawnScripted(scheduler, self =>
            {
                self.WakeCondition = WakeCondition.Writable(fullPipe);
                return ProcessState.Sleeping;
            }).Process;
            var runner = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(runner.StepCount, Is.EqualTo(9));

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(runner.StepCount, Is.EqualTo(19));
        }

        [Test]
        public void Kill_during_wake_probe_does_not_resurrect_the_sleeper()
        {
            var scheduler = new Scheduler(10);
            var sleeperPid = 0;
            var stateSeenAfterKill = ProcessState.Running;
            var exitCodeSeenAfterKill = 0;
            var maliciousStream = new KillOnProbeStream(() =>
            {
                scheduler.Kill(sleeperPid);
                stateSeenAfterKill = scheduler.GetState(sleeperPid);
                exitCodeSeenAfterKill = scheduler.GetExitCode(sleeperPid);
            });
            var (pid, sleeper) = SpawnScripted(scheduler, self =>
            {
                self.WakeCondition = WakeCondition.Readable(maliciousStream);
                return ProcessState.Sleeping;
            });
            sleeperPid = pid;
            var runner = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();

            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(stateSeenAfterKill, Is.EqualTo(ProcessState.Finished));
            Assert.That(exitCodeSeenAfterKill, Is.EqualTo(Scheduler.InterruptExitCode));
            Assert.That(scheduler.Contains(sleeperPid), Is.False);
            Assert.That(runner.StepCount, Is.EqualTo(9));

            scheduler.Tick();
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(runner.StepCount, Is.EqualTo(19));
        }

        [Test]
        public void Probe_budget_caps_stream_probes_per_tick_and_resets_on_the_next_tick()
        {
            var scheduler = new Scheduler(tickBudget: 10, probeBudget: 3);
            var countingStream = new CountingStream(new PipeStream());
            var sleeper = SpawnScripted(scheduler, self =>
            {
                self.WakeCondition = WakeCondition.Readable(countingStream);
                return ProcessState.Sleeping;
            }).Process;
            var runner = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();
            Assert.That(runner.StepCount, Is.EqualTo(9));
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(countingStream.ReadCount, Is.EqualTo(3));

            scheduler.Tick();
            Assert.That(runner.StepCount, Is.EqualTo(19));
            Assert.That(sleeper.StepCount, Is.EqualTo(1));
            Assert.That(countingStream.ReadCount, Is.EqualTo(6));
        }

        [Test]
        public void Corpse_is_reaped_even_when_a_step_throws()
        {
            var scheduler = new Scheduler(10);
            var corpsePid = SpawnScripted(scheduler, RunForever).Pid;
            scheduler.Kill(corpsePid);
            var throwerPid = SpawnScripted(
                scheduler,
                self => throw new InvalidOperationException("step failure")).Pid;

            var stepFailure = Assert.Throws<InvalidOperationException>(() => scheduler.Tick());

            Assert.That(stepFailure.Message, Does.Contain("step failure"));
            Assert.That(scheduler.Contains(corpsePid), Is.False);

            scheduler.Kill(throwerPid);
            scheduler.Tick();
            Assert.That(scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Kill_unknown_pid_throws()
        {
            var scheduler = new Scheduler();

            Assert.Throws<ArgumentException>(() => scheduler.Kill(999));
        }

        [Test]
        public void Kill_between_ticks_finishes_the_process_with_interrupt_exit_code()
        {
            var scheduler = new Scheduler(10);
            var (pid, process) = SpawnScripted(scheduler, RunForever);
            scheduler.Tick();
            var stepsBeforeKill = process.StepCount;

            scheduler.Kill(pid);

            Assert.That(scheduler.GetState(pid), Is.EqualTo(ProcessState.Finished));
            Assert.That(scheduler.GetExitCode(pid), Is.EqualTo(130));
            Assert.That(Scheduler.InterruptExitCode, Is.EqualTo(130));

            scheduler.Tick();
            Assert.That(process.StepCount, Is.EqualTo(stepsBeforeKill));
            Assert.That(scheduler.Contains(pid), Is.False);
        }

        [Test]
        public void Kill_on_a_finished_corpse_is_a_noop_that_preserves_the_exit_code()
        {
            var scheduler = new Scheduler(10);
            var pid = SpawnScripted(scheduler, RunForever).Pid;
            scheduler.Kill(pid);

            Assert.That(() => scheduler.Kill(pid), Throws.Nothing);
            Assert.That(scheduler.GetExitCode(pid), Is.EqualTo(Scheduler.InterruptExitCode));
        }

        [Test]
        public void Reap_keeps_the_cursor_aligned_when_the_middle_process_finishes_mid_run()
        {
            var scheduler = new Scheduler(4);
            var first = SpawnScripted(scheduler, RunForever).Process;
            var (middlePid, middle) = SpawnScripted(scheduler, self =>
            {
                self.ExitCode = 0;
                return ProcessState.Finished;
            });
            var last = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();
            Assert.That(first.StepCount, Is.EqualTo(2));
            Assert.That(middle.StepCount, Is.EqualTo(1));
            Assert.That(last.StepCount, Is.EqualTo(1));
            Assert.That(scheduler.Contains(middlePid), Is.False);

            scheduler.Tick();
            Assert.That(first.StepCount, Is.EqualTo(4));
            Assert.That(middle.StepCount, Is.EqualTo(1));
            Assert.That(last.StepCount, Is.EqualTo(3));
        }

        [Test]
        public void Constructor_rejects_non_positive_budget()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Scheduler(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Scheduler(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Scheduler(10, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new Scheduler(10, -3));
        }

        [Test]
        public void Spawn_rejects_null_arguments()
        {
            var scheduler = new Scheduler();
            var command = new StubCommand((spawnContext, spawnArguments) => new ScriptedProcess(spawnContext, RunForever));
            var context = CreateContext();

            Assert.Throws<ArgumentNullException>(() => scheduler.Spawn(null, context, NoArguments));
            Assert.Throws<ArgumentNullException>(() => scheduler.Spawn(command, null, NoArguments));
            Assert.Throws<ArgumentNullException>(() => scheduler.Spawn(command, context, null));
        }

        [Test]
        public void Spawn_throws_when_the_command_creates_a_null_process()
        {
            var scheduler = new Scheduler();
            var command = new StubCommand((spawnContext, spawnArguments) => null);

            Assert.Throws<InvalidOperationException>(() => scheduler.Spawn(command, CreateContext(), NoArguments));
        }

        [Test]
        public void Get_exit_code_on_a_running_process_throws()
        {
            var scheduler = new Scheduler();
            var pid = SpawnScripted(scheduler, RunForever).Pid;

            Assert.Throws<InvalidOperationException>(() => scheduler.GetExitCode(pid));
        }

        [Test]
        public void Corpse_queries_on_unknown_pid_throw()
        {
            var scheduler = new Scheduler();

            Assert.Throws<ArgumentException>(() => scheduler.GetState(999));
            Assert.Throws<ArgumentException>(() => scheduler.GetExitCode(999));
        }

        [Test]
        public void Reentrant_tick_throws()
        {
            var scheduler = new Scheduler(10);
            SpawnScripted(scheduler, self =>
            {
                scheduler.Tick();
                return ProcessState.Finished;
            });

            var reentrancyError = Assert.Throws<InvalidOperationException>(() => scheduler.Tick());
            Assert.That(reentrancyError.Message, Does.Contain("already in progress"));
        }

        [Test]
        public void Spawn_process_overload_assigns_increasing_pids_and_never_steps()
        {
            var scheduler = new Scheduler();
            var first = new ScriptedProcess(CreateContext(), RunForever);
            var second = new ScriptedProcess(CreateContext(), RunForever);

            var firstPid = scheduler.Spawn(first, "alpha");
            var secondPid = scheduler.Spawn(second, "beta");

            Assert.That(secondPid, Is.GreaterThan(firstPid));
            Assert.That(first.StepCount, Is.EqualTo(0));
            Assert.That(second.StepCount, Is.EqualTo(0));
        }

        [Test]
        public void Spawn_process_overload_rejects_null_and_empty_arguments()
        {
            var scheduler = new Scheduler();
            var process = new ScriptedProcess(CreateContext(), RunForever);

            Assert.Throws<ArgumentNullException>(() => scheduler.Spawn(null, "name"));
            Assert.Throws<ArgumentNullException>(() => scheduler.Spawn(process, null));
            Assert.Throws<ArgumentException>(() => scheduler.Spawn(process, ""));
        }

        [Test]
        public void Spawn_during_step_is_safe_and_does_not_step_the_new_process_at_spawn()
        {
            var scheduler = new Scheduler(4);
            ScriptedProcess child = null;
            var childStepsSeenAtSpawn = -1;
            var spawner = SpawnScripted(scheduler, self =>
            {
                if (child == null)
                {
                    child = new ScriptedProcess(CreateContext(), RunForever);
                    scheduler.Spawn(child, "child");
                    childStepsSeenAtSpawn = child.StepCount;
                }

                return ProcessState.Running;
            }).Process;
            var runner = SpawnScripted(scheduler, RunForever).Process;

            scheduler.Tick();

            Assert.That(childStepsSeenAtSpawn, Is.EqualTo(0));
            Assert.That(spawner.StepCount + runner.StepCount + child.StepCount, Is.EqualTo(4));

            scheduler.Tick();
            Assert.That(spawner.StepCount + runner.StepCount + child.StepCount, Is.EqualTo(8));
            Assert.That(child.StepCount, Is.GreaterThan(0));
        }

        [Test]
        public void Retained_status_survives_ticks_until_collected()
        {
            var scheduler = new Scheduler(10);
            var pid = SpawnScripted(scheduler, self =>
            {
                self.ExitCode = 7;
                return ProcessState.Finished;
            }).Pid;

            scheduler.Tick();
            scheduler.Tick();
            Assert.That(scheduler.Contains(pid), Is.False);

            Assert.That(scheduler.TryPeekExitCode(pid, out var peeked), Is.True);
            Assert.That(peeked, Is.EqualTo(7));
            Assert.That(scheduler.TryPeekExitCode(pid, out _), Is.True);

            Assert.That(scheduler.TryCollectExitCode(pid, out var collected), Is.True);
            Assert.That(collected, Is.EqualTo(7));
            Assert.That(scheduler.TryPeekExitCode(pid, out _), Is.False);
            Assert.That(scheduler.TryCollectExitCode(pid, out _), Is.False);
        }

        [Test]
        public void Status_collected_during_the_finishing_tick_does_not_resurrect_after_reap()
        {
            var scheduler = new Scheduler(10);
            var finishingPid = SpawnScripted(scheduler, self =>
            {
                self.ExitCode = 7;
                return ProcessState.Finished;
            }).Pid;
            var collectedInTick = -1;
            SpawnScripted(scheduler, self =>
            {
                if (scheduler.TryCollectExitCode(finishingPid, out var code))
                {
                    collectedInTick = code;
                    return ProcessState.Finished;
                }

                return ProcessState.Running;
            });

            scheduler.Tick();

            Assert.That(collectedInTick, Is.EqualTo(7));
            Assert.That(scheduler.TryPeekExitCode(finishingPid, out _), Is.False);
        }

        [Test]
        public void Kill_interrupt_code_is_collectible_after_reap()
        {
            var scheduler = new Scheduler(10);
            var pid = SpawnScripted(scheduler, RunForever).Pid;

            scheduler.Kill(pid);
            scheduler.Tick();
            Assert.That(scheduler.Contains(pid), Is.False);

            Assert.That(scheduler.TryCollectExitCode(pid, out var code), Is.True);
            Assert.That(code, Is.EqualTo(Scheduler.InterruptExitCode));
        }

        [Test]
        public void Peek_and_collect_are_false_for_running_and_unknown_pids()
        {
            var scheduler = new Scheduler(10);
            var runningPid = SpawnScripted(scheduler, RunForever).Pid;

            Assert.That(scheduler.TryPeekExitCode(runningPid, out _), Is.False);
            Assert.That(scheduler.TryCollectExitCode(runningPid, out _), Is.False);
            Assert.That(scheduler.TryPeekExitCode(9999, out _), Is.False);
            Assert.That(scheduler.TryCollectExitCode(9999, out _), Is.False);
        }

        [Test]
        public void Retained_status_does_not_leak_into_contains_or_get_exit_code()
        {
            var scheduler = new Scheduler(10);
            var pid = SpawnScripted(scheduler, self =>
            {
                self.ExitCode = 7;
                return ProcessState.Finished;
            }).Pid;

            scheduler.Tick();

            Assert.That(scheduler.TryPeekExitCode(pid, out _), Is.True);
            Assert.That(scheduler.Contains(pid), Is.False);
            Assert.Throws<ArgumentException>(() => scheduler.GetExitCode(pid));
        }

        [Test]
        public void List_processes_returns_live_entries_pid_ascending_excluding_reaped()
        {
            var scheduler = new Scheduler(10);
            var firstPid = scheduler.Spawn(new ScriptedProcess(CreateContext(), RunForever), "alpha");
            var finishingPid = SpawnScripted(scheduler, self =>
            {
                self.ExitCode = 0;
                return ProcessState.Finished;
            }).Pid;
            var lastPid = scheduler.Spawn(new ScriptedProcess(CreateContext(), RunForever), "omega");

            scheduler.Tick();
            var processes = scheduler.ListProcesses();

            Assert.That(processes.Count, Is.EqualTo(2));
            Assert.That(processes[0].Pid, Is.EqualTo(firstPid));
            Assert.That(processes[0].Name, Is.EqualTo("alpha"));
            Assert.That(processes[1].Pid, Is.EqualTo(lastPid));
            Assert.That(processes[1].Name, Is.EqualTo("omega"));
            Assert.That(processes[0].State, Is.EqualTo(ProcessState.Running));
            Assert.That(scheduler.Contains(finishingPid), Is.False);
        }

        [Test]
        public void List_processes_uses_the_command_name_for_the_command_overload()
        {
            var scheduler = new Scheduler();
            var pid = SpawnScripted(scheduler, RunForever).Pid;

            var processes = scheduler.ListProcesses();

            Assert.That(processes.Count, Is.EqualTo(1));
            Assert.That(processes[0].Pid, Is.EqualTo(pid));
            Assert.That(processes[0].Name, Is.EqualTo("stub"));
        }

        [Test]
        public void List_processes_carries_the_context_owner_uid()
        {
            var scheduler = new Scheduler();
            var descriptors = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
            var context = new ExecutionContext("/", new Credentials(42), new Dictionary<string, string>(), descriptors);
            scheduler.Spawn(new ScriptedProcess(context, RunForever), "owned");

            var processes = scheduler.ListProcesses();

            Assert.That(processes[0].OwnerUid, Is.EqualTo(42));
        }

        private sealed class KillOnProbeStream : IByteStream
        {
            private readonly Action onProbe;

            public KillOnProbeStream(Action onProbe)
            {
                this.onProbe = onProbe;
            }

            public StreamResult Read(byte[] buffer, int offset, int count)
            {
                onProbe();
                return StreamResult.Eof;
            }

            public StreamResult Write(byte[] buffer, int offset, int count)
            {
                onProbe();
                return StreamResult.Eof;
            }

            public void CloseRead()
            {
            }

            public void CloseWrite()
            {
            }
        }
    }
}
