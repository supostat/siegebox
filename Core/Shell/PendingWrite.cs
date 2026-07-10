using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>A byte payload being pushed into one stream across as many Steps as backpressure requires.</summary>
    internal sealed class PendingWrite
    {
        private readonly byte[] bytes;
        private int position;

        public PendingWrite(IByteStream target, byte[] bytes)
        {
            Target = target;
            this.bytes = bytes;
        }

        public IByteStream Target { get; }

        public DrainStatus Advance()
        {
            while (position < bytes.Length)
            {
                var result = Target.Write(bytes, position, bytes.Length - position);
                switch (result.Status)
                {
                    case StreamStatus.Ok:
                        position += result.Count;
                        break;
                    case StreamStatus.WouldBlock:
                        return DrainStatus.WouldBlock;
                    default:
                        return DrainStatus.Closed;
                }
            }

            return DrainStatus.Completed;
        }
    }
}
