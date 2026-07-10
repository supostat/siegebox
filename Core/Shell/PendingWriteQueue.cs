using System.Collections.Generic;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// An ordered set of payloads draining into their streams under backpressure. On
    /// WouldBlock, <see cref="BlockedTarget"/> is the stream to sleep on.
    /// </summary>
    internal sealed class PendingWriteQueue
    {
        private readonly Queue<PendingWrite> writes = new Queue<PendingWrite>();

        public IByteStream? BlockedTarget { get; private set; }

        public void Enqueue(IByteStream target, string text)
        {
            if (text.Length == 0)
            {
                return;
            }

            writes.Enqueue(new PendingWrite(target, Encoding.UTF8.GetBytes(text)));
        }

        public DrainStatus Drain()
        {
            while (writes.Count > 0)
            {
                var current = writes.Peek();
                switch (current.Advance())
                {
                    case DrainStatus.Completed:
                        writes.Dequeue();
                        break;
                    case DrainStatus.WouldBlock:
                        BlockedTarget = current.Target;
                        return DrainStatus.WouldBlock;
                    default:
                        return DrainStatus.Closed;
                }
            }

            return DrainStatus.Completed;
        }
    }
}
