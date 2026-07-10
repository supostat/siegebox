using Siegebox.Vfs;

namespace Siegebox.Process.Tests
{
    internal sealed class CountingStream : IByteStream
    {
        private readonly IByteStream inner;

        public CountingStream(IByteStream inner)
        {
            this.inner = inner;
        }

        public int ReadCount { get; private set; }

        public int WriteCount { get; private set; }

        public StreamResult Read(byte[] buffer, int offset, int count)
        {
            ReadCount++;
            return inner.Read(buffer, offset, count);
        }

        public StreamResult Write(byte[] buffer, int offset, int count)
        {
            WriteCount++;
            return inner.Write(buffer, offset, count);
        }

        public void CloseRead() => inner.CloseRead();

        public void CloseWrite() => inner.CloseWrite();
    }
}
