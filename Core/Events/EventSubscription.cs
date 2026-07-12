using System;

namespace Siegebox.Events
{
    /// <summary>Handle for one bus subscription: Dispose unsubscribes and is idempotent.</summary>
    public sealed class EventSubscription : IDisposable
    {
        private readonly EventBus bus;
        private bool disposed;

        internal EventSubscription(EventBus bus, string eventName, Action<KernelEvent> handler)
        {
            this.bus = bus;
            EventName = eventName;
            Handler = handler;
        }

        internal string EventName { get; }

        internal Action<KernelEvent> Handler { get; }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            bus.Remove(this);
        }
    }
}
