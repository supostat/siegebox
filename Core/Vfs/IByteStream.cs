namespace Siegebox.Vfs
{
    public interface IByteStream
    {
        StreamResult Read(byte[] buffer, int offset, int count);

        StreamResult Write(byte[] buffer, int offset, int count);

        void CloseRead();

        void CloseWrite();
    }
}
