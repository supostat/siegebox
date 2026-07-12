using System;
using NUnit.Framework;

namespace Siegebox.App.Tests
{
    [TestFixture]
    public sealed class AppRegistryTests
    {
        private static AppDescriptor Descriptor(string id)
            => new AppDescriptor(id, id, () => new FakeApp());

        [Test]
        public void Registered_descriptor_round_trips()
        {
            var registry = new AppRegistry();
            var descriptor = Descriptor("files");

            registry.Register(descriptor);

            Assert.That(registry.TryGet("files", out var found), Is.True);
            Assert.That(found, Is.SameAs(descriptor));
        }

        [Test]
        public void Descriptors_are_sorted_by_id_ordinal()
        {
            var registry = new AppRegistry();
            registry.Register(Descriptor("zeta"));
            registry.Register(Descriptor("alpha"));
            registry.Register(Descriptor("mike"));

            var descriptors = registry.Descriptors;
            Assert.That(descriptors, Has.Count.EqualTo(3));
            Assert.That(descriptors[0].Id, Is.EqualTo("alpha"));
            Assert.That(descriptors[1].Id, Is.EqualTo("mike"));
            Assert.That(descriptors[2].Id, Is.EqualTo("zeta"));
        }

        [Test]
        public void Unknown_id_is_not_found()
        {
            var registry = new AppRegistry();

            Assert.That(registry.TryGet("absent", out _), Is.False);
        }

        [Test]
        public void Duplicate_id_is_rejected()
        {
            var registry = new AppRegistry();
            registry.Register(Descriptor("twin"));

            Assert.Throws<ArgumentException>(() => registry.Register(Descriptor("twin")));
        }

        [Test]
        public void Null_arguments_are_rejected()
        {
            var registry = new AppRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register(null));
            Assert.Throws<ArgumentNullException>(() => registry.TryGet(null, out _));
            Assert.Throws<ArgumentNullException>(() => registry.Unregister(null));
        }

        [Test]
        public void Unregistered_app_is_gone_and_can_be_registered_again()
        {
            var registry = new AppRegistry();
            registry.Register(Descriptor("files"));

            registry.Unregister("files");

            Assert.That(registry.TryGet("files", out _), Is.False);
            Assert.That(() => registry.Register(Descriptor("files")), Throws.Nothing);
            Assert.That(registry.TryGet("files", out _), Is.True);
        }

        [Test]
        public void Unregister_of_an_absent_app_throws()
        {
            var registry = new AppRegistry();

            Assert.Throws<ArgumentException>(() => registry.Unregister("absent"));
        }
    }
}
