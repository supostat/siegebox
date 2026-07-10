namespace Siegebox.Vfs
{
    /// <summary>
    /// Decorator for shared terminal endpoints whose lifetime is owned by the host:
    /// closing a descriptor table must never close the terminal underneath the others.
    /// </summary>
    internal sealed class NonClosingStream : IByteStream
    {
        private readonly IByteStream inner;

        public NonClosingStream(IByteStream inner)
        {
            this.inner = inner;
        }

        public StreamResult Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public StreamResult Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);

        public void CloseRead()
        {
        }

        public void CloseWrite()
        {
        }
    }
}
