using System;
using System.Collections.Generic;

namespace Siegebox.App
{
    /// <summary>Id-keyed catalog of launchable apps; Descriptors lists every entry sorted by id.</summary>
    public sealed class AppRegistry
    {
        private readonly Dictionary<string, AppDescriptor> descriptors =
            new Dictionary<string, AppDescriptor>(StringComparer.Ordinal);

        public void Register(AppDescriptor descriptor)
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (descriptors.ContainsKey(descriptor.Id))
            {
                throw new ArgumentException($"App '{descriptor.Id}' is already registered.", nameof(descriptor));
            }

            descriptors.Add(descriptor.Id, descriptor);
        }

        public void Unregister(string id)
        {
            if (id is null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (!descriptors.Remove(id))
            {
                throw new ArgumentException($"App '{id}' is not registered.", nameof(id));
            }
        }

        public bool TryGet(string id, out AppDescriptor descriptor)
        {
            if (id is null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return descriptors.TryGetValue(id, out descriptor!);
        }

        public IReadOnlyList<AppDescriptor> Descriptors
        {
            get
            {
                var sorted = new List<AppDescriptor>(descriptors.Values);
                sorted.Sort((left, right) => StringComparer.Ordinal.Compare(left.Id, right.Id));
                return sorted;
            }
        }
    }
}
