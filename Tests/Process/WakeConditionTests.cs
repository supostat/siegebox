using System;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class WakeConditionTests
    {
        [Test]
        public void None_and_default_are_inert()
        {
            Assert.That(WakeCondition.None.Kind, Is.EqualTo(WakeConditionKind.None));
            Assert.That(default(WakeCondition).Kind, Is.EqualTo(WakeConditionKind.None));
        }

        [Test]
        public void Readable_captures_the_stream()
        {
            var pipe = new PipeStream();

            var condition = WakeCondition.Readable(pipe);

            Assert.That(condition.Kind, Is.EqualTo(WakeConditionKind.StreamReadable));
            Assert.That(condition.Stream, Is.SameAs(pipe));
        }

        [Test]
        public void Writable_captures_the_stream()
        {
            var pipe = new PipeStream();

            var condition = WakeCondition.Writable(pipe);

            Assert.That(condition.Kind, Is.EqualTo(WakeConditionKind.StreamWritable));
            Assert.That(condition.Stream, Is.SameAs(pipe));
        }

        [Test]
        public void Process_exit_captures_the_pid()
        {
            var condition = WakeCondition.ProcessExit(5);

            Assert.That(condition.Kind, Is.EqualTo(WakeConditionKind.ProcessExit));
            Assert.That(condition.Pid, Is.EqualTo(5));
        }

        [Test]
        public void Readable_rejects_null_stream()
        {
            Assert.Throws<ArgumentNullException>(() => WakeCondition.Readable(null));
        }

        [Test]
        public void Writable_rejects_null_stream()
        {
            Assert.Throws<ArgumentNullException>(() => WakeCondition.Writable(null));
        }

        [Test]
        public void Process_exit_rejects_non_positive_pid()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => WakeCondition.ProcessExit(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => WakeCondition.ProcessExit(-1));
        }
    }
}
