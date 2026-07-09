using System;

namespace Siegebox.Vfs
{
    public readonly struct StreamResult
    {
        private StreamResult(StreamStatus status, int count)
        {
            Status = status;
            Count = count;
        }

        public StreamStatus Status { get; }

        public int Count { get; }

        public bool IsOk => Status == StreamStatus.Ok;

        public static StreamResult Ok(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be non-negative.");
            }

            return new StreamResult(StreamStatus.Ok, count);
        }

        public static StreamResult WouldBlock => new StreamResult(StreamStatus.WouldBlock, 0);

        public static StreamResult Eof => new StreamResult(StreamStatus.Eof, 0);
    }
}
