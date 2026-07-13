using System.IO;
using System.Text;

namespace Siegebox.Vfs
{
    /// <summary>
    /// Drains an <see cref="IByteStream"/> to the end of the stream and decodes the accumulated
    /// bytes as UTF-8. Reading stops at the first non-Ok status (end of stream or a closed
    /// stream); the caller retains ownership of the stream and is responsible for closing it.
    /// </summary>
    internal static class ByteStreamText
    {
        private const int ChunkBytes = 4096;

        public static string ReadToEnd(IByteStream stream)
        {
            var collected = new MemoryStream();
            var chunk = new byte[ChunkBytes];
            while (true)
            {
                var result = stream.Read(chunk, 0, chunk.Length);
                if (result.Status != StreamStatus.Ok)
                {
                    return Encoding.UTF8.GetString(collected.ToArray());
                }

                collected.Write(chunk, 0, result.Count);
            }
        }
    }
}
