using System;
using NUnit.Framework;

namespace Siegebox.App.Tests
{
    [TestFixture]
    public sealed class FileTypeRegistryTests
    {
        [Test]
        public void Registered_file_type_round_trips()
        {
            var registry = new FileTypeRegistry();

            registry.Register("txt", "editor");

            Assert.That(registry.TryGet("txt", out var appId), Is.True);
            Assert.That(appId, Is.EqualTo("editor"));
        }

        [Test]
        public void File_types_are_sorted_ordinal()
        {
            var registry = new FileTypeRegistry();
            registry.Register("zip", "archiver");
            registry.Register("lua", "editor");
            registry.Register("txt", "editor");

            Assert.That(registry.FileTypes, Is.EqualTo(new[] { "lua", "txt", "zip" }));
        }

        [Test]
        public void Re_registering_an_extension_overrides_the_previous_mapping()
        {
            var registry = new FileTypeRegistry();
            registry.Register("txt", "base-editor");

            registry.Register("txt", "mod-editor");

            Assert.That(registry.TryGet("txt", out var appId), Is.True);
            Assert.That(appId, Is.EqualTo("mod-editor"));
            Assert.That(registry.FileTypes, Has.Count.EqualTo(1));
        }

        [Test]
        public void Extensions_are_normalized_to_lowercase_without_the_leading_dot()
        {
            var registry = new FileTypeRegistry();

            registry.Register(".TXT", "editor");

            Assert.That(registry.TryGet("txt", out var appId), Is.True);
            Assert.That(appId, Is.EqualTo("editor"));
            Assert.That(registry.TryGet(".Txt", out _), Is.True);
            Assert.That(registry.FileTypes, Is.EqualTo(new[] { "txt" }));
        }

        [Test]
        public void Unknown_extension_is_not_found()
        {
            var registry = new FileTypeRegistry();

            Assert.That(registry.TryGet("absent", out _), Is.False);
        }

        [Test]
        public void Unregistered_file_type_is_gone()
        {
            var registry = new FileTypeRegistry();
            registry.Register("txt", "editor");

            registry.Unregister(".TXT");

            Assert.That(registry.TryGet("txt", out _), Is.False);
        }

        [Test]
        public void Unregister_of_an_absent_file_type_throws()
        {
            var registry = new FileTypeRegistry();

            Assert.Throws<ArgumentException>(() => registry.Unregister("absent"));
        }

        [Test]
        public void Null_and_blank_arguments_are_rejected()
        {
            var registry = new FileTypeRegistry();

            Assert.Throws<ArgumentNullException>(() => registry.Register(null, "editor"));
            Assert.Throws<ArgumentNullException>(() => registry.Register("txt", null));
            Assert.Throws<ArgumentException>(() => registry.Register("", "editor"));
            Assert.Throws<ArgumentException>(() => registry.Register(".", "editor"));
            Assert.Throws<ArgumentException>(() => registry.Register("txt", " "));
            Assert.Throws<ArgumentNullException>(() => registry.Unregister(null));
            Assert.Throws<ArgumentNullException>(() => registry.TryGet(null, out _));
        }
    }
}
