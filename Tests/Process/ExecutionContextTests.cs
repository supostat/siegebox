using System;
using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class ExecutionContextTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static FileDescriptorTable CreateDescriptors()
        {
            return new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
        }

        [Test]
        public void Constructor_stores_working_directory_credentials_and_descriptors()
        {
            var descriptors = CreateDescriptors();

            var context = new ExecutionContext("/home", Root, new Dictionary<string, string>(), descriptors);

            Assert.That(context.WorkingDirectory, Is.EqualTo("/home"));
            Assert.That(context.Credentials, Is.SameAs(Root));
            Assert.That(context.FileDescriptors, Is.SameAs(descriptors));
        }

        [Test]
        public void Environment_is_defensively_copied()
        {
            var source = new Dictionary<string, string> { ["PATH"] = "/bin" };

            var context = new ExecutionContext("/", Root, source, CreateDescriptors());
            source["PATH"] = "/tampered";
            source["HOME"] = "/injected";

            Assert.That(context.Environment["PATH"], Is.EqualTo("/bin"));
            Assert.That(context.Environment.ContainsKey("HOME"), Is.False);
        }

        [Test]
        public void Environment_cannot_be_mutated_through_a_downcast()
        {
            var source = new Dictionary<string, string> { ["PATH"] = "/bin" };
            var context = new ExecutionContext("/", Root, source, CreateDescriptors());

            var downcast = (IDictionary<string, string>)context.Environment;

            Assert.Throws<NotSupportedException>(() => downcast["PATH"] = "/tampered");
            Assert.That(context.Environment["PATH"], Is.EqualTo("/bin"));
        }

        [Test]
        public void Constructor_rejects_null_working_directory()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ExecutionContext(null, Root, new Dictionary<string, string>(), CreateDescriptors()));
        }

        [Test]
        public void Constructor_rejects_empty_or_whitespace_working_directory()
        {
            Assert.Throws<ArgumentException>(
                () => new ExecutionContext("", Root, new Dictionary<string, string>(), CreateDescriptors()));
            Assert.Throws<ArgumentException>(
                () => new ExecutionContext("   ", Root, new Dictionary<string, string>(), CreateDescriptors()));
        }

        [Test]
        public void Constructor_rejects_null_credentials()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ExecutionContext("/", null, new Dictionary<string, string>(), CreateDescriptors()));
        }

        [Test]
        public void Constructor_rejects_null_environment()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ExecutionContext("/", Root, null, CreateDescriptors()));
        }

        [Test]
        public void Constructor_rejects_null_file_descriptors()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ExecutionContext("/", Root, new Dictionary<string, string>(), null));
        }
    }
}
