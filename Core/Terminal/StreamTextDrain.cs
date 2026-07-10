using System;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Terminal
{
    /// <summary>
    /// Pulls whatever bytes a stream currently offers and decodes them as UTF-8 text.
    /// The decoder persists across calls, so a multi-byte character split between two
    /// drains decodes exactly once, when it completes.
    /// </summary>
    public sealed class StreamTextDrain
    {
        private const int ChunkSize = 1024;

        private readonly IByteStream source;
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
        private readonly byte[] bytes = new byte[ChunkSize];
        private readonly char[] characters = new char[Encoding.UTF8.GetMaxCharCount(ChunkSize)];

        public StreamTextDrain(IByteStream source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }

        public string Drain()
        {
            StringBuilder? text = null;
            while (true)
            {
                var result = source.Read(bytes, 0, ChunkSize);
                if (result.Status != StreamStatus.Ok)
                {
                    return text?.ToString() ?? "";
                }

                var decoded = decoder.GetChars(bytes, 0, result.Count, characters, 0);
                if (decoded > 0)
                {
                    (text ??= new StringBuilder()).Append(characters, 0, decoded);
                }
            }
        }
    }
}
