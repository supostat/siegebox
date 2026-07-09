using System;

namespace Siegebox.Vfs
{
    internal sealed class NullStream : IByteStream
    {
        public StreamResult Read(byte[] buffer, int offset, int count)
        {
            ValidateRange(buffer, offset, count);
            return StreamResult.Eof;
        }

        public StreamResult Write(byte[] buffer, int offset, int count)
        {
            ValidateRange(buffer, offset, count);
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
