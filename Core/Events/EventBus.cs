using System;
using System.Collections.Generic;

namespace Siegebox.Events
{
    /// <summary>
    /// Name-keyed pub/sub for kernel hooks. Publish delivers to a snapshot of the current
    /// subscribers, so unsubscribing during a publish is safe. A subscriber can never break
    /// the publisher: handler exceptions are contained and forwarded to the error sink.
    /// Re-entrant publishes (a handler triggering another publish, directly or through the
    /// kernel) nest up to <see cref="MaxPublishDepth"/> levels; a deeper publish is dropped
    /// without dispatching and reported to the error sink, so a feedback loop between a
    /// handler and the kernel can never overflow the native stack.
    /// </summary>
    public sealed class EventBus
    {
        public const int MaxPublishDepth = 8;

        private readonly Dictionary<string, List<EventSubscription>> subscriptionsByEventName =
            new Dictionary<string, List<EventSubscription>>(StringComparer.Ordinal);

        private readonly Action<KernelEvent, Exception>? handlerErrorSink;

        private int publishDepth;

        public EventBus(Action<KernelEvent, Exception>? handlerErrorSink = null)
        {
            this.handlerErrorSink = handlerErrorSink;
        }

        public EventSubscription Subscribe(string eventName, Action<KernelEvent> handler)
        {
            if (eventName is null)
            {
                throw new ArgumentNullException(nameof(eventName));
            }

            if (handler is null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            if (!subscriptionsByEventName.TryGetValue(eventName, out var subscriptions))
            {
                subscriptions = new List<EventSubscription>();
                subscriptionsByEventName.Add(eventName, subscriptions);
            }

            var subscription = new EventSubscription(this, eventName, handler);
            subscriptions.Add(subscription);
            return subscription;
        }

        public void Publish(KernelEvent kernelEvent)
        {
            if (kernelEvent is null)
            {
                throw new ArgumentNullException(nameof(kernelEvent));
            }

            if (publishDepth >= MaxPublishDepth)
            {
                handlerErrorSink?.Invoke(
                    kernelEvent,
                    new InvalidOperationException(
                        $"Event '{kernelEvent.Name}' was dropped: re-entrant publish depth exceeded {MaxPublishDepth}."));
                return;
            }

            if (!subscriptionsByEventName.TryGetValue(kernelEvent.Name, out var subscriptions))
            {
                return;
            }

            publishDepth++;
            try
            {
                foreach (var subscription in subscriptions.ToArray())
                {
                    DispatchContained(subscription, kernelEvent);
                }
            }
            finally
            {
                publishDepth--;
            }
        }

        private void DispatchContained(EventSubscription subscription, KernelEvent kernelEvent)
        {
            try
            {
                subscription.Handler(kernelEvent);
            }
            catch (Exception handlerError)
            {
                handlerErrorSink?.Invoke(kernelEvent, handlerError);
            }
        }

        internal void Remove(EventSubscription subscription)
        {
            if (subscriptionsByEventName.TryGetValue(subscription.EventName, out var subscriptions))
            {
                subscriptions.Remove(subscription);
            }
        }
    }
}
