using System;
using NUnit.Framework;

namespace Siegebox.App.Tests
{
    [TestFixture]
    public sealed class AppDescriptorTests
    {
        [Test]
        public void Exposes_id_and_display_name()
        {
            var descriptor = new AppDescriptor("files", "file manager", () => new FakeApp());

            Assert.That(descriptor.Id, Is.EqualTo("files"));
            Assert.That(descriptor.DisplayName, Is.EqualTo("file manager"));
        }

        [Test]
        public void Create_instance_builds_a_fresh_app_each_time()
        {
            var descriptor = new AppDescriptor("files", "files", () => new FakeApp());

            var first = descriptor.CreateInstance();
            var second = descriptor.CreateInstance();

            Assert.That(second, Is.Not.SameAs(first));
        }

        [Test]
        public void Whitespace_only_id_or_display_name_is_rejected()
        {
            Assert.Throws<ArgumentException>(() => new AppDescriptor("  ", "files", () => new FakeApp()));
            Assert.Throws<ArgumentException>(() => new AppDescriptor("files", "  ", () => new FakeApp()));
        }

        [Test]
        public void Null_arguments_are_rejected()
        {
            Assert.Throws<ArgumentNullException>(() => new AppDescriptor(null, "files", () => new FakeApp()));
            Assert.Throws<ArgumentNullException>(() => new AppDescriptor("files", null, () => new FakeApp()));
            Assert.Throws<ArgumentNullException>(() => new AppDescriptor("files", "files", null));
        }

        [Test]
        public void Factory_returning_null_fails_at_create()
        {
            var descriptor = new AppDescriptor("files", "files", () => null);

            Assert.Throws<InvalidOperationException>(() => descriptor.CreateInstance());
        }
    }
}
