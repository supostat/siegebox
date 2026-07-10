using System;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class PipeStreamProbeContractTests
    {
        private static readonly byte[] ProbeBuffer = Array.Empty<byte>();

        [Test]
        public void Probe_read_on_empty_pipe_with_open_writer_reports_would_block()
        {
            var pipe = new PipeStream();

            Assert.That(pipe.Read(ProbeBuffer, 0, 0).Status, Is.EqualTo(StreamStatus.WouldBlock));
        }

        [Test]
        public void Probe_read_with_buffered_data_reports_ok_and_leaves_the_data_readable()
        {
            var pipe = new PipeStream();
            var payload = new byte[] { 1, 2, 3 };
            pipe.Write(payload, 0, payload.Length);

            var probe = pipe.Read(ProbeBuffer, 0, 0);

            Assert.That(probe.Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(probe.Count, Is.EqualTo(0));
            var drained = new byte[payload.Length];
            Assert.That(pipe.Read(drained, 0, drained.Length).Count, Is.EqualTo(payload.Length));
            Assert.That(drained, Is.EqualTo(payload));
        }

        [Test]
        public void Probe_read_after_close_write_reports_eof()
        {
            var pipe = new PipeStream();
            pipe.CloseWrite();

            Assert.That(pipe.Read(ProbeBuffer, 0, 0).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void Probe_write_on_full_pipe_reports_would_block()
        {
            var pipe = new PipeStream(capacity: 2);
            pipe.Write(new byte[] { 1, 2 }, 0, 2);

            Assert.That(pipe.Write(ProbeBuffer, 0, 0).Status, Is.EqualTo(StreamStatus.WouldBlock));
        }

        [Test]
        public void Probe_write_after_close_read_reports_eof()
        {
            var pipe = new PipeStream();
            pipe.CloseRead();

            Assert.That(pipe.Write(ProbeBuffer, 0, 0).Status, Is.EqualTo(StreamStatus.Eof));
        }
    }
}
