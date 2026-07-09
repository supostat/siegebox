using System;

namespace Siegebox.Vfs
{
    internal sealed class FileContent : IFileContent
    {
        private const int InitialCapacity = 16;

        private byte[] bytes = Array.Empty<byte>();
        private int length;

        public FileContent()
        {
        }

        public FileContent(byte[] initialBytes)
        {
            if (initialBytes.Length > 0)
            {
                WriteAt(0, initialBytes, 0, initialBytes.Length);
            }
        }

        public int Length => length;

        public int ReadAt(int position, byte[] destination, int offset, int count)
        {
            var available = length - position;
            if (available <= 0)
            {
                return 0;
            }

            var toRead = Math.Min(count, available);
            Array.Copy(bytes, position, destination, offset, toRead);
            return toRead;
        }

        public void WriteAt(int position, byte[] source, int offset, int count)
        {
            EnsureCapacity(position + count);
            Array.Copy(source, offset, bytes, position, count);
            if (position + count > length)
            {
                length = position + count;
            }
        }

        public byte[] Snapshot()
        {
            var copy = new byte[length];
            Array.Copy(bytes, copy, length);
            return copy;
        }

        private void EnsureCapacity(int required)
        {
            if (bytes.Length >= required)
            {
                return;
            }

            var capacity = bytes.Length == 0 ? InitialCapacity : bytes.Length * 2;
            while (capacity < required)
            {
                capacity *= 2;
            }

            Array.Resize(ref bytes, capacity);
        }
    }
}
