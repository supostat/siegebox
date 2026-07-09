using System;
using System.Collections.Generic;

namespace Siegebox.Vfs
{
    public sealed class PipeStream : IByteStream
    {
        public const int DefaultCapacity = 65536;

        private readonly Queue<byte> buffer = new Queue<byte>();
        private readonly int capacity;
        private bool readClosed;
        private bool writeClosed;

        public PipeStream(int capacity = DefaultCapacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
            }

            this.capacity = capacity;
        }

        public StreamResult Read(byte[] destination, int offset, int count)
        {
            ValidateRange(destination, offset, count);
            if (buffer.Count == 0)
            {
                return writeClosed ? StreamResult.Eof : StreamResult.WouldBlock;
            }

            var toRead = Math.Min(count, buffer.Count);
            for (var index = 0; index < toRead; index++)
            {
                destination[offset + index] = buffer.Dequeue();
            }

            return StreamResult.Ok(toRead);
        }

        public StreamResult Write(byte[] source, int offset, int count)
        {
            ValidateRange(source, offset, count);
            if (readClosed)
            {
                return StreamResult.Eof;
            }

            var space = capacity - buffer.Count;
            if (space == 0)
            {
                return StreamResult.WouldBlock;
            }

            var toWrite = Math.Min(count, space);
            for (var index = 0; index < toWrite; index++)
            {
                buffer.Enqueue(source[offset + index]);
            }

            return StreamResult.Ok(toWrite);
        }

        public void CloseRead() => readClosed = true;

        public void CloseWrite() => writeClosed = true;

        private static void ValidateRange(byte[] buffer, int offset, int count)
        {
            if (buffer is null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset > buffer.Length - count)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "The requested range falls outside the buffer.");
            }
        }
    }
}
