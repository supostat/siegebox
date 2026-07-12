using System;

namespace Siegebox.App
{
    /// <summary>A launchable app: its registry id, its taskbar display name and its instance factory.</summary>
    public sealed class AppDescriptor
    {
        private readonly Func<IApp> factory;

        public AppDescriptor(string id, string displayName, Func<IApp> factory)
        {
            if (id is null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("An app id must not be blank.", nameof(id));
            }

            if (displayName is null)
            {
                throw new ArgumentNullException(nameof(displayName));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException("An app display name must not be blank.", nameof(displayName));
            }

            this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }

        public string DisplayName { get; }

        public IApp CreateInstance()
        {
            var app = factory();
            if (app is null)
            {
                throw new InvalidOperationException($"App '{Id}' factory returned null.");
            }

            return app;
        }
    }
}
