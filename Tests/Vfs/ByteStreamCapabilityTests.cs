using System;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class ByteStreamCapabilityTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        [Test]
        public void FileStream_writes_are_read_back_in_order()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_000_000), Root);
            var writer = vfs.Open("/f", OpenMode.Write, Root);
            var payload = Encoding.UTF8.GetBytes("stream-bytes");
            writer.Write(payload, 0, payload.Length);
            writer.CloseWrite();

            var reader = vfs.Open("/f", OpenMode.Read, Root);
            Assert.That(Drain(reader), Is.EqualTo(payload));
        }

        [Test]
        public void NullStream_discards_writes_and_reports_eof_on_read()
        {
            var vfs = new VirtualFileSystem();
            var stream = vfs.Open("/dev/null", OpenMode.ReadWrite, Root);
            var payload = new byte[] { 1, 2, 3, 4 };
            var write = stream.Write(payload, 0, payload.Length);
            Assert.That(write.Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(write.Count, Is.EqualTo(4));
            Assert.That(stream.Read(new byte[4], 0, 4).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void FileStream_opened_for_reading_rejects_writes()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_000_000), Root);
            var reader = vfs.Open("/f", OpenMode.Read, Root);
            Assert.Throws<InvalidOperationException>(() => reader.Write(new byte[] { 1 }, 0, 1));
        }

        [Test]
        public void FileStream_opened_for_writing_rejects_reads()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_000_000), Root);
            var writer = vfs.Open("/f", OpenMode.Write, Root);
            Assert.Throws<InvalidOperationException>(() => writer.Read(new byte[1], 0, 1));
        }

        [Test]
        public void PipeStream_reports_eof_on_write_after_reader_close()
        {
            var pipe = new PipeStream();
            pipe.CloseRead();
            var payload = new byte[] { 1, 2 };
            Assert.That(pipe.Write(payload, 0, payload.Length).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void PipeStream_round_trips_written_bytes()
        {
            var pipe = new PipeStream();
            var payload = new byte[] { 10, 20, 30 };
            Assert.That(pipe.Write(payload, 0, 3).Count, Is.EqualTo(3));
            var buffer = new byte[3];
            var read = pipe.Read(buffer, 0, 3);
            Assert.That(read.Count, Is.EqualTo(3));
            Assert.That(buffer, Is.EqualTo(payload));
        }

        [Test]
        public void PipeStream_reports_would_block_when_empty_and_writer_is_open()
        {
            var pipe = new PipeStream();
            Assert.That(pipe.Read(new byte[4], 0, 4).Status, Is.EqualTo(StreamStatus.WouldBlock));
        }

        [Test]
        public void PipeStream_applies_backpressure_when_full_without_dropping_bytes()
        {
            var pipe = new PipeStream(capacity: 4);
            Assert.That(pipe.Write(new byte[] { 1, 2, 3, 4 }, 0, 4).Count, Is.EqualTo(4));
            Assert.That(pipe.Write(new byte[] { 5 }, 0, 1).Status, Is.EqualTo(StreamStatus.WouldBlock));

            var drained = new byte[2];
            pipe.Read(drained, 0, 2);
            Assert.That(pipe.Write(new byte[] { 5, 6 }, 0, 2).Count, Is.EqualTo(2));
        }

        [Test]
        public void PipeStream_drains_remaining_bytes_then_reports_eof_after_writer_close()
        {
            var pipe = new PipeStream();
            pipe.Write(new byte[] { 7, 8 }, 0, 2);
            pipe.CloseWrite();
            var buffer = new byte[8];
            Assert.That(pipe.Read(buffer, 0, 8).Count, Is.EqualTo(2));
            Assert.That(pipe.Read(buffer, 0, 8).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void Stream_capability_remains_usable_after_the_node_is_chmodded_shut()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_000_000), Root);
            vfs.Chown("/f", 100, 500, Root);
            var owner = User(100, 500);

            var writer = vfs.Open("/f", OpenMode.Write, owner);
            var payload = Encoding.UTF8.GetBytes("kept");
            writer.Write(payload, 0, payload.Length);
            writer.CloseWrite();

            var reader = vfs.Open("/f", OpenMode.Read, owner);
            vfs.Chmod("/f", Mode(0), owner);

            Assert.That(Drain(reader), Is.EqualTo(payload));
            var denied = Assert.Throws<VfsException>(() => vfs.Open("/f", OpenMode.Read, owner));
            Assert.That(denied.Error, Is.EqualTo(VfsError.EACCES));
        }

        private static byte[] Drain(IByteStream stream)
        {
            var output = new MemoryStream();
            var buffer = new byte[64];
            while (true)
            {
                var result = stream.Read(buffer, 0, buffer.Length);
                if (result.Status != StreamStatus.Ok)
                {
                    break;
                }
                output.Write(buffer, 0, result.Count);
            }
            return output.ToArray();
        }
    }
}
