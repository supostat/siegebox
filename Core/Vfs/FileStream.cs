using System;

namespace Siegebox.Vfs
{
    internal sealed class FileStream : IByteStream
    {
        private readonly VfsNode node;
        private readonly bool canRead;
        private readonly bool canWrite;
        private int position;

        public FileStream(VfsNode node, OpenMode mode)
        {
            this.node = node;
            canRead = mode != OpenMode.Write;
            canWrite = mode != OpenMode.Read;
        }

        public StreamResult Read(byte[] buffer, int offset, int count)
        {
            if (!canRead)
            {
                throw new InvalidOperationException("This stream was not opened for reading.");
            }

            ValidateRange(buffer, offset, count);
            var available = node.ContentLength - position;
            if (available <= 0)
            {
                return StreamResult.Eof;
            }

            var toRead = Math.Min(count, available);
            node.ReadContent(position, buffer, offset, toRead);
            position += toRead;
            return StreamResult.Ok(toRead);
        }

        public StreamResult Write(byte[] buffer, int offset, int count)
        {
            if (!canWrite)
            {
                throw new InvalidOperationException("This stream was not opened for writing.");
            }

            ValidateRange(buffer, offset, count);
            node.WriteContent(position, buffer, offset, count);
            position += count;
            return StreamResult.Ok(count);
        }

        public void CloseRead()
        {
        }

        public void CloseWrite()
        {
        }

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
