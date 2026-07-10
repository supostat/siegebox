using System.Text;
using NUnit.Framework;
using Siegebox.Terminal;
using Siegebox.Vfs;

namespace Siegebox.Terminal.Tests
{
    [TestFixture]
    public sealed class StreamTextDrainTests
    {
        private static void WriteAll(PipeStream pipe, byte[] bytes, int offset, int count)
        {
            var result = pipe.Write(bytes, offset, count);
            Assert.That(result.Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(result.Count, Is.EqualTo(count));
        }

        [Test]
        public void Drains_available_bytes_to_a_string()
        {
            var pipe = new PipeStream();
            var drain = new StreamTextDrain(pipe);
            var bytes = Encoding.UTF8.GetBytes("hello");
            WriteAll(pipe, bytes, 0, bytes.Length);

            Assert.That(drain.Drain(), Is.EqualTo("hello"));
            Assert.That(drain.Drain(), Is.EqualTo(""));
        }

        [Test]
        public void Returns_empty_on_would_block_and_on_eof()
        {
            var openPipe = new PipeStream();
            Assert.That(new StreamTextDrain(openPipe).Drain(), Is.EqualTo(""));

            var closedPipe = new PipeStream();
            closedPipe.CloseWrite();
            Assert.That(new StreamTextDrain(closedPipe).Drain(), Is.EqualTo(""));
        }

        [Test]
        public void Split_two_byte_character_decodes_once_it_completes()
        {
            var pipe = new PipeStream();
            var drain = new StreamTextDrain(pipe);
            var bytes = Encoding.UTF8.GetBytes("π");
            Assert.That(bytes.Length, Is.EqualTo(2));

            WriteAll(pipe, bytes, 0, 1);
            Assert.That(drain.Drain(), Is.EqualTo(""));

            WriteAll(pipe, bytes, 1, 1);
            Assert.That(drain.Drain(), Is.EqualTo("π"));
        }

        [Test]
        public void Split_four_byte_character_decodes_once_it_completes()
        {
            var pipe = new PipeStream();
            var drain = new StreamTextDrain(pipe);
            var bytes = Encoding.UTF8.GetBytes("\U0001F600");
            Assert.That(bytes.Length, Is.EqualTo(4));

            WriteAll(pipe, bytes, 0, 2);
            Assert.That(drain.Drain(), Is.EqualTo(""));

            WriteAll(pipe, bytes, 2, 2);
            Assert.That(drain.Drain(), Is.EqualTo("\U0001F600"));
        }

        [Test]
        public void Drains_more_than_one_chunk()
        {
            var pipe = new PipeStream();
            var drain = new StreamTextDrain(pipe);
            var text = new string('a', 3000);
            var bytes = Encoding.UTF8.GetBytes(text);
            WriteAll(pipe, bytes, 0, bytes.Length);

            Assert.That(drain.Drain(), Is.EqualTo(text));
        }
    }
}
