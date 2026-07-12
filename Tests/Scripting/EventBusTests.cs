using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Events;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class EventBusTests
    {
        [Test]
        public void Subscribed_handler_receives_the_published_payload()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.ProcessSpawnedName, received.Add);

            bus.Publish(KernelEvent.ProcessSpawned(3, "probe", 42));

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Name, Is.EqualTo("process.spawned"));
            Assert.That(received[0].Pid, Is.EqualTo(3));
            Assert.That(received[0].ProcessName, Is.EqualTo("probe"));
            Assert.That(received[0].Uid, Is.EqualTo(42));
            Assert.That(received[0].ExitCode, Is.EqualTo(0));
            Assert.That(received[0].Path, Is.EqualTo(""));
            Assert.That(received[0].Access, Is.EqualTo(""));
        }

        [Test]
        public void Two_subscribers_run_in_subscription_order()
        {
            var bus = new EventBus();
            var order = new List<string>();
            bus.Subscribe(KernelEvent.FileOpenedName, _ => order.Add("first"));
            bus.Subscribe(KernelEvent.FileOpenedName, _ => order.Add("second"));

            bus.Publish(KernelEvent.FileOpened("/etc/motd", 0, "read"));

            Assert.That(order, Is.EqualTo(new[] { "first", "second" }));
        }

        [Test]
        public void Publish_reaches_only_subscribers_of_the_event_name()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.ProcessExitedName, received.Add);

            bus.Publish(KernelEvent.ProcessSpawned(1, "probe", 0));

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void Publish_with_no_subscribers_is_a_no_op()
        {
            var bus = new EventBus();

            Assert.That(() => bus.Publish(KernelEvent.ProcessExited(1, "probe", 0)), Throws.Nothing);
        }

        [Test]
        public void Disposed_subscription_stops_delivery_and_dispose_is_idempotent()
        {
            var bus = new EventBus();
            var received = new List<KernelEvent>();
            var subscription = bus.Subscribe(KernelEvent.ProcessSpawnedName, received.Add);

            subscription.Dispose();
            subscription.Dispose();
            bus.Publish(KernelEvent.ProcessSpawned(1, "probe", 0));

            Assert.That(received, Is.Empty);
        }

        [Test]
        public void Unsubscribe_during_publish_is_safe_and_still_delivers_the_current_event()
        {
            var bus = new EventBus();
            var received = new List<string>();
            EventSubscription second = null;
            bus.Subscribe(KernelEvent.ProcessSpawnedName, _ =>
            {
                received.Add("first");
                second.Dispose();
            });
            second = bus.Subscribe(KernelEvent.ProcessSpawnedName, _ => received.Add("second"));

            bus.Publish(KernelEvent.ProcessSpawned(1, "probe", 0));
            bus.Publish(KernelEvent.ProcessSpawned(2, "probe", 0));

            Assert.That(received, Is.EqualTo(new[] { "first", "second", "first" }));
        }

        [Test]
        public void Throwing_subscriber_does_not_stop_the_others_and_the_sink_receives_the_failure()
        {
            var failures = new List<(KernelEvent Event, Exception Error)>();
            var bus = new EventBus((failedEvent, error) => failures.Add((failedEvent, error)));
            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.FileOpenedName, _ => throw new InvalidOperationException("handler boom"));
            bus.Subscribe(KernelEvent.FileOpenedName, received.Add);

            var published = KernelEvent.FileOpened("/etc/motd", 7, "read");
            bus.Publish(published);

            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(failures[0].Event, Is.SameAs(published));
            Assert.That(failures[0].Error.Message, Is.EqualTo("handler boom"));
        }

        [Test]
        public void Subscriber_republishing_its_own_event_is_cut_off_at_the_depth_cap_and_reported()
        {
            var failures = new List<(KernelEvent Event, Exception Error)>();
            var bus = new EventBus((failedEvent, error) => failures.Add((failedEvent, error)));
            var deliveries = 0;
            bus.Subscribe(KernelEvent.FileOpenedName, _ =>
            {
                deliveries++;
                bus.Publish(KernelEvent.FileOpened("/loop", 0, "read"));
            });

            Assert.That(() => bus.Publish(KernelEvent.FileOpened("/loop", 0, "read")), Throws.Nothing);

            Assert.That(deliveries, Is.EqualTo(EventBus.MaxPublishDepth));
            Assert.That(failures, Has.Count.EqualTo(1));
            Assert.That(failures[0].Event.Path, Is.EqualTo("/loop"));
            Assert.That(failures[0].Error.Message, Does.Contain("re-entrant publish depth"));
        }

        [Test]
        public void Publishing_again_after_a_depth_cut_off_dispatches_normally()
        {
            var bus = new EventBus();
            var deliveries = 0;
            var subscription = bus.Subscribe(KernelEvent.FileOpenedName, _ =>
            {
                deliveries++;
                bus.Publish(KernelEvent.FileOpened("/loop", 0, "read"));
            });
            bus.Publish(KernelEvent.FileOpened("/loop", 0, "read"));
            subscription.Dispose();

            var received = new List<KernelEvent>();
            bus.Subscribe(KernelEvent.FileOpenedName, received.Add);
            bus.Publish(KernelEvent.FileOpened("/after", 0, "read"));

            Assert.That(deliveries, Is.EqualTo(EventBus.MaxPublishDepth));
            Assert.That(received, Has.Count.EqualTo(1));
            Assert.That(received[0].Path, Is.EqualTo("/after"));
        }

        [Test]
        public void Null_arguments_are_rejected()
        {
            var bus = new EventBus();

            Assert.Throws<ArgumentNullException>(() => bus.Subscribe(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe(KernelEvent.FileOpenedName, null));
            Assert.Throws<ArgumentNullException>(() => bus.Publish(null));
        }

        [Test]
        public void Event_factories_populate_only_their_documented_fields()
        {
            var exited = KernelEvent.ProcessExited(9, "spin", 130);
            Assert.That(exited.Name, Is.EqualTo("process.exited"));
            Assert.That(exited.Pid, Is.EqualTo(9));
            Assert.That(exited.ProcessName, Is.EqualTo("spin"));
            Assert.That(exited.ExitCode, Is.EqualTo(130));
            Assert.That(exited.Uid, Is.EqualTo(0));

            var opened = KernelEvent.FileOpened("/notes.txt", 100, "write");
            Assert.That(opened.Name, Is.EqualTo("file.opened"));
            Assert.That(opened.Path, Is.EqualTo("/notes.txt"));
            Assert.That(opened.Uid, Is.EqualTo(100));
            Assert.That(opened.Access, Is.EqualTo("write"));
            Assert.That(opened.Pid, Is.EqualTo(0));
            Assert.That(opened.ProcessName, Is.EqualTo(""));
        }
    }
}
