using System;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    [TestFixture]
    public sealed class FileDescriptorTableTests
    {
        [Test]
        public void Standard_descriptors_are_zero_one_two()
        {
            Assert.That(FileDescriptorTable.Stdin, Is.EqualTo(0));
            Assert.That(FileDescriptorTable.Stdout, Is.EqualTo(1));
            Assert.That(FileDescriptorTable.Stderr, Is.EqualTo(2));
        }

        [Test]
        public void Get_returns_the_exact_streams_handed_to_the_constructor()
        {
            var stdin = new PipeStream();
            var stdout = new PipeStream();
            var stderr = new PipeStream();
            var table = new FileDescriptorTable(stdin, stdout, stderr);

            Assert.That(table.Get(FileDescriptorTable.Stdin), Is.SameAs(stdin));
            Assert.That(table.Get(FileDescriptorTable.Stdout), Is.SameAs(stdout));
            Assert.That(table.Get(FileDescriptorTable.Stderr), Is.SameAs(stderr));
        }

        [Test]
        public void Table_mediates_reads_and_writes()
        {
            var stdin = new PipeStream();
            var stdout = new PipeStream();
            var table = new FileDescriptorTable(stdin, stdout, new PipeStream());

            stdin.Write(new byte[] { 7, 8 }, 0, 2);
            var received = new byte[2];
            Assert.That(table.Get(FileDescriptorTable.Stdin).Read(received, 0, 2).Count, Is.EqualTo(2));
            Assert.That(received, Is.EqualTo(new byte[] { 7, 8 }));

            table.Get(FileDescriptorTable.Stdout).Write(new byte[] { 9 }, 0, 1);
            var echoed = new byte[1];
            Assert.That(stdout.Read(echoed, 0, 1).Count, Is.EqualTo(1));
            Assert.That(echoed[0], Is.EqualTo(9));
        }

        [Test]
        public void Get_unknown_descriptor_throws()
        {
            var table = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());

            Assert.Throws<ArgumentException>(() => table.Get(3));
        }

        [Test]
        public void Constructor_rejects_null_streams()
        {
            var pipe = new PipeStream();

            Assert.Throws<ArgumentNullException>(() => new FileDescriptorTable(null, pipe, pipe));
            Assert.Throws<ArgumentNullException>(() => new FileDescriptorTable(pipe, null, pipe));
            Assert.Throws<ArgumentNullException>(() => new FileDescriptorTable(pipe, pipe, null));
        }

        [Test]
        public void Close_all_closes_stdin_for_reading_only()
        {
            var stdin = new PipeStream();
            var table = new FileDescriptorTable(stdin, new PipeStream(), new PipeStream());

            table.CloseAll();

            Assert.That(stdin.Write(new byte[] { 1 }, 0, 1).Status, Is.EqualTo(StreamStatus.Eof));
            Assert.That(stdin.Read(new byte[1], 0, 1).Status, Is.EqualTo(StreamStatus.WouldBlock));
        }

        [Test]
        public void Close_all_closes_stdout_for_writing_and_buffered_bytes_still_drain()
        {
            var stdout = new PipeStream();
            var table = new FileDescriptorTable(new PipeStream(), stdout, new PipeStream());
            table.Get(FileDescriptorTable.Stdout).Write(new byte[] { 1, 2 }, 0, 2);

            table.CloseAll();

            var drained = new byte[4];
            Assert.That(stdout.Read(drained, 0, 4).Count, Is.EqualTo(2));
            Assert.That(stdout.Read(drained, 0, 4).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void Close_all_leaves_the_read_side_of_write_descriptors_open()
        {
            var stdout = new PipeStream();
            var stderr = new PipeStream();
            var table = new FileDescriptorTable(new PipeStream(), stdout, stderr);

            table.CloseAll();

            Assert.That(stdout.Read(new byte[1], 0, 1).Status, Is.EqualTo(StreamStatus.Eof));
            Assert.That(stderr.Read(new byte[1], 0, 1).Status, Is.EqualTo(StreamStatus.Eof));
            Assert.That(stdout.Write(new byte[] { 1 }, 0, 1).Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(stderr.Write(new byte[] { 2 }, 0, 1).Status, Is.EqualTo(StreamStatus.Ok));
        }

        [Test]
        public void Close_all_isolates_a_throwing_stream_and_still_closes_the_remaining_descriptors()
        {
            var stdin = new PipeStream();
            var stderr = new PipeStream();
            var table = new FileDescriptorTable(stdin, new ThrowOnCloseWriteStream(), stderr);

            var closeFailure = Assert.Throws<InvalidOperationException>(() => table.CloseAll());

            Assert.That(closeFailure.Message, Does.Contain("close-write failure"));
            Assert.That(stdin.Write(new byte[] { 1 }, 0, 1).Status, Is.EqualTo(StreamStatus.Eof));
            Assert.That(stderr.Read(new byte[1], 0, 1).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void Close_all_twice_is_safe()
        {
            var table = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());
            table.CloseAll();

            Assert.That(() => table.CloseAll(), Throws.Nothing);
        }

        [Test]
        public void Clone_returns_a_distinct_table_sharing_the_same_streams()
        {
            var stdin = new PipeStream();
            var stdout = new PipeStream();
            var stderr = new PipeStream();
            var table = new FileDescriptorTable(stdin, stdout, stderr);

            var clone = table.Clone();

            Assert.That(clone, Is.Not.SameAs(table));
            Assert.That(clone.Get(FileDescriptorTable.Stdin), Is.SameAs(stdin));
            Assert.That(clone.Get(FileDescriptorTable.Stdout), Is.SameAs(stdout));
            Assert.That(clone.Get(FileDescriptorTable.Stderr), Is.SameAs(stderr));
        }

        [Test]
        public void Repeated_clones_are_independent_instances()
        {
            var table = new FileDescriptorTable(new PipeStream(), new PipeStream(), new PipeStream());

            var first = table.Clone();
            var second = table.Clone();

            Assert.That(first, Is.Not.SameAs(second));
            Assert.That(first.Get(FileDescriptorTable.Stdout), Is.SameAs(table.Get(FileDescriptorTable.Stdout)));
            Assert.That(second.Get(FileDescriptorTable.Stdout), Is.SameAs(table.Get(FileDescriptorTable.Stdout)));
        }

        private sealed class ThrowOnCloseWriteStream : IByteStream
        {
            public StreamResult Read(byte[] buffer, int offset, int count) => StreamResult.WouldBlock;

            public StreamResult Write(byte[] buffer, int offset, int count) => StreamResult.Ok(count);

            public void CloseRead()
            {
            }

            public void CloseWrite() => throw new InvalidOperationException("close-write failure");
        }
    }
}
