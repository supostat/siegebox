using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Events;
using Siegebox.Process;
using Siegebox.Process.Tests;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class KernelHookTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static ExecutionContext CreateContext(int uid = 0)
        {
            var descriptors = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
            return new ExecutionContext("/", new Credentials(uid), new Dictionary<string, string>(), descriptors);
        }

        [Test]
        public void Scheduler_spawn_publishes_process_spawned_with_pid_name_and_uid()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.ProcessSpawnedName, received.Add);
            var scheduler = new Scheduler(events: bus);

            var pid = scheduler.Spawn(new ScriptedProcess(CreateContext(42), _ => ProcessState.Running), "worker");

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Pid, Is.EqualTo(pid));
            Assert.That(received[0].ProcessName, Is.EqualTo("worker"));
            Assert.That(received[0].Uid, Is.EqualTo(42));
        }

        [Test]
        public void Finished_process_publishes_process_exited_with_its_exit_code()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.ProcessExitedName, received.Add);
            var scheduler = new Scheduler(events: bus);
            var pid = scheduler.Spawn(
                new ScriptedProcess(CreateContext(), process =>
                {
                    process.ExitCode = 0;
                    return ProcessState.Finished;
                }),
                "quick");

            scheduler.Tick();

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Pid, Is.EqualTo(pid));
            Assert.That(received[0].ProcessName, Is.EqualTo("quick"));
            Assert.That(received[0].ExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Kill_between_ticks_publishes_process_exited_130()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.ProcessExitedName, received.Add);
            var scheduler = new Scheduler(events: bus);
            var pid = scheduler.Spawn(new ScriptedProcess(CreateContext(), _ => ProcessState.Running), "spin");

            scheduler.Kill(pid);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Pid, Is.EqualTo(pid));
            Assert.That(received[0].ExitCode, Is.EqualTo(Scheduler.InterruptExitCode));
        }

        [Test]
        public void Subscriber_spawning_and_killing_during_an_exit_publish_inside_tick_is_safe()
        {
            var bus = new EventBus();
            var scheduler = new Scheduler(events: bus);
            var victimPid = scheduler.Spawn(new ScriptedProcess(CreateContext(), _ => ProcessState.Running), "victim");
            var reentrantPid = 0;
            bus.Subscribe(KernelEvent.ProcessExitedName, exited =>
            {
                if (exited.ProcessName != "quick")
                {
                    return;
                }

                reentrantPid = scheduler.Spawn(new ScriptedProcess(CreateContext(), _ => ProcessState.Running), "reentrant");
                scheduler.Kill(victimPid);
            });
            scheduler.Spawn(
                new ScriptedProcess(CreateContext(), process =>
                {
                    process.ExitCode = 0;
                    return ProcessState.Finished;
                }),
                "quick");

            Assert.That(() => scheduler.Tick(), Throws.Nothing);

            Assert.That(scheduler.Contains(reentrantPid), Is.True);
            Assert.That(scheduler.TryPeekExitCode(victimPid, out var victimExitCode), Is.True);
            Assert.That(victimExitCode, Is.EqualTo(Scheduler.InterruptExitCode));
        }

        [Test]
        public void Throwing_exit_subscriber_does_not_break_tick_and_the_exit_code_is_retained()
        {
            var failures = new List<Exception>();
            var bus = new EventBus((_, error) => failures.Add(error));
            var scheduler = new Scheduler(events: bus);
            bus.Subscribe(KernelEvent.ProcessExitedName, _ => throw new InvalidOperationException("mod handler boom"));
            var pid = scheduler.Spawn(
                new ScriptedProcess(CreateContext(), process =>
                {
                    process.ExitCode = 7;
                    return ProcessState.Finished;
                }),
                "quick");

            Assert.That(() => scheduler.Tick(), Throws.Nothing);

            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(scheduler.TryPeekExitCode(pid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(7));
        }

        [Test]
        public void Successful_open_publishes_file_opened_with_path_uid_and_access()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.FileOpenedName, received.Add);
            var vfs = new VirtualFileSystem(bus);
            vfs.CreateFile("/notes.txt", new PermissionMode(0b110_100_100), Root);

            vfs.Open("/notes.txt", OpenMode.Read, new Credentials(7));

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Path, Is.EqualTo("/notes.txt"));
            Assert.That(received[0].Uid, Is.EqualTo(7));
            Assert.That(received[0].Access, Is.EqualTo("read"));
        }

        [Test]
        public void Denied_open_publishes_nothing()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.FileOpenedName, received.Add);
            var vfs = new VirtualFileSystem(bus);
            vfs.CreateFile("/secret", new PermissionMode(0b110_000_000), Root);

            var denied = Assert.Throws<VfsException>(() => vfs.Open("/secret", OpenMode.Read, new Credentials(7)));

            Assert.That(denied.Error, Is.EqualTo(VfsError.EACCES));
            Assert.That(received, Is.Empty);
        }

        [Test]
        public void Open_for_write_of_an_existing_file_publishes_file_opened_write()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.FileOpenedName, received.Add);
            var vfs = new VirtualFileSystem(bus);
            vfs.CreateFile("/log.txt", new PermissionMode(0b110_100_100), Root);

            vfs.OpenForWrite("/log.txt", WriteBehavior.Append, new PermissionMode(0b110_100_100), Root);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Path, Is.EqualTo("/log.txt"));
            Assert.That(received[0].Access, Is.EqualTo("write"));
        }

        [Test]
        public void Open_for_write_creating_the_file_publishes_exactly_one_event()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.FileOpenedName, received.Add);
            var vfs = new VirtualFileSystem(bus);

            vfs.OpenForWrite("/fresh.txt", WriteBehavior.Truncate, new PermissionMode(0b110_100_100), Root);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Path, Is.EqualTo("/fresh.txt"));
            Assert.That(received[0].Access, Is.EqualTo("write"));
        }
    }
}
