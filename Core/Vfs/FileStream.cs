using System;

namespace Siegebox.Vfs
{
    internal sealed class FileStream : IByteStream
    {
        private readonly IFileContent content;
        private readonly bool canRead;
        private readonly bool canWrite;
        private readonly bool append;
        private int position;

        public FileStream(IFileContent content, OpenMode mode, bool append = false)
        {
            this.content = content;
            this.append = append;
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
            if (position >= content.Length)
            {
                return StreamResult.Eof;
            }

            var read = content.ReadAt(position, buffer, offset, count);
            position += read;
            return StreamResult.Ok(read);
        }

        public StreamResult Write(byte[] buffer, int offset, int count)
        {
            if (!canWrite)
            {
                throw new InvalidOperationException("This stream was not opened for writing.");
            }

            ValidateRange(buffer, offset, count);
            var writePosition = append ? content.Length : position;
            content.WriteAt(writePosition, buffer, offset, count);
            position = writePosition + count;
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
