using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class ScriptApiSubscriptionTests
    {
        private sealed class SubscriptionEnvironment
        {
            public SubscriptionEnvironment()
            {
                Failures = new List<Exception>();
                Log = new List<string>();
                Bus = new EventBus((_, error) => Failures.Add(error));
                var api = new ScriptApi(
                    new CommandRegistry(),
                    new AppRegistry(),
                    new FileTypeRegistry(),
                    new VirtualFileSystem(Bus),
                    Bus,
                    Log.Add);
                Host = new LuaHost();
                api.InstallInto(Host, api.CreateScope());
            }

            public List<Exception> Failures { get; }

            public List<string> Log { get; }

            public EventBus Bus { get; }

            public LuaHost Host { get; }
        }

        [Test]
        public void Subscribe_delivers_published_events_to_the_lua_handler()
        {
            var environment = new SubscriptionEnvironment();
            environment.Host.RunChunk(
                "siegebox.subscribe('file.opened', function(evt) siegebox.log('opened:' .. evt.path .. ':' .. evt.access) end)",
                "subscribe");

            environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read"));

            Assert.That(environment.Log, Is.EqualTo(new[] { "opened:/note.txt:read" }));
        }

        [Test]
        public void Unsubscribe_function_stops_delivery_and_is_idempotent()
        {
            var environment = new SubscriptionEnvironment();
            environment.Host.RunChunk(
                "local unsubscribe = siegebox.subscribe('file.opened', function(evt) siegebox.log('seen') end)" +
                " unsubscribe() unsubscribe()",
                "unsubscribe");

            environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read"));

            Assert.That(environment.Log, Is.Empty);
        }

        [Test]
        public void Unknown_event_name_raises_an_error()
        {
            var environment = new SubscriptionEnvironment();

            var result = environment.Host.RunChunk(
                "local ok, err = pcall(function() siegebox.subscribe('nope.event', function() end) end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "unknownevent");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("nope.event"));
        }

        [Test]
        public void Subscriber_reading_the_vfs_from_a_file_opened_handler_is_cut_off_at_the_depth_cap()
        {
            var environment = new SubscriptionEnvironment();
            environment.Host.RunChunk(
                "siegebox.vfs.write('/note.txt', 'ahoy')" +
                " siegebox.subscribe('file.opened', function(evt) siegebox.log(siegebox.vfs.read('/note.txt')) end)",
                "reentrant");

            Assert.That(() => environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read")), Throws.Nothing);

            Assert.That(environment.Log, Has.Count.EqualTo(EventBus.MaxPublishDepth));
            Assert.That(environment.Log, Has.All.EqualTo("ahoy"));
            Assert.That(environment.Failures, Has.Count.EqualTo(1));
            Assert.That(environment.Failures[0].Message, Does.Contain("re-entrant publish depth"));
        }

        [Test]
        public void Subscriber_budget_is_far_smaller_than_the_chunk_budget()
        {
            var environment = new SubscriptionEnvironment();
            environment.Host.RunChunk("function busywork() for i = 1, 200000 do end end", "define");
            environment.Host.RunChunk("busywork()", "fullbudget");
            environment.Host.RunChunk("siegebox.subscribe('file.opened', function(evt) busywork() end)", "hook");

            environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read"));

            Assert.That(environment.Failures, Has.Count.EqualTo(1));
            Assert.That(environment.Failures[0], Is.InstanceOf<LuaBudgetExceededException>());
            Assert.That(((LuaBudgetExceededException)environment.Failures[0]).ChunkName, Is.EqualTo("subscribe:file.opened"));
        }

        [Test]
        public void Infinite_loop_subscriber_is_contained_and_reported_as_budget_exhaustion()
        {
            var environment = new SubscriptionEnvironment();
            environment.Host.RunChunk(
                "siegebox.subscribe('file.opened', function(evt) while true do end end)",
                "spinhook");

            Assert.That(() => environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read")), Throws.Nothing);

            Assert.That(environment.Failures, Has.Count.EqualTo(1));
            Assert.That(environment.Failures[0], Is.InstanceOf<LuaBudgetExceededException>());
        }

        [Test]
        public void Throwing_subscriber_is_contained_by_the_bus()
        {
            var environment = new SubscriptionEnvironment();
            environment.Host.RunChunk(
                "siegebox.subscribe('file.opened', function(evt) error('handler boom') end)",
                "throwing");

            Assert.That(() => environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read")), Throws.Nothing);

            Assert.That(environment.Failures, Has.Count.EqualTo(1));
            Assert.That(environment.Failures[0].Message, Does.Contain("handler boom"));
        }
    }
}
