using System.Collections.Generic;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// A cooperative, byte-at-a-time line reader over a stdin stream. It reads ONE byte per
    /// attempt and accumulates until a newline (or end of stream) so an interactive process
    /// consumes exactly one line and never over-reads into the shell's next command. Returns
    /// false while the line is still incomplete (the caller sleeps on Readable and retries); on
    /// completion it yields the decoded line with a trailing carriage return stripped.
    /// </summary>
    internal sealed class LineReader
    {
        private readonly List<byte> lineBytes = new List<byte>();
        private readonly byte[] oneByte = new byte[1];

        public bool TryReadLine(IByteStream stream, out string line)
        {
            line = string.Empty;
            while (true)
            {
                var result = stream.Read(oneByte, 0, 1);
                if (result.Status == StreamStatus.WouldBlock || (result.Status == StreamStatus.Ok && result.Count == 0))
                {
                    return false;
                }

                if (result.Status == StreamStatus.Eof || oneByte[0] == (byte)'\n')
                {
                    line = DecodeLine();
                    return true;
                }

                lineBytes.Add(oneByte[0]);
            }
        }

        private string DecodeLine()
        {
            var text = Encoding.UTF8.GetString(lineBytes.ToArray()).TrimEnd('\r');
            lineBytes.Clear();
            return text;
        }
    }
}
