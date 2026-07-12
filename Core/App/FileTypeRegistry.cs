using System;
using System.Collections.Generic;

namespace Siegebox.App
{
    /// <summary>
    /// Extension-keyed map from file types to app ids. Unlike the other registries,
    /// Register OVERRIDES an existing mapping — last registration wins, so mods override
    /// base content. Keys are normalized: leading dot stripped, lowercased.
    /// </summary>
    public sealed class FileTypeRegistry
    {
        private readonly Dictionary<string, string> appIdsByExtension =
            new Dictionary<string, string>(StringComparer.Ordinal);

        public void Register(string extension, string appId)
        {
            var key = NormalizeKey(extension);
            if (appId is null)
            {
                throw new ArgumentNullException(nameof(appId));
            }

            if (string.IsNullOrWhiteSpace(appId))
            {
                throw new ArgumentException("An app id must not be blank.", nameof(appId));
            }

            appIdsByExtension[key] = appId;
        }

        public void Unregister(string extension)
        {
            var key = NormalizeKey(extension);
            if (!appIdsByExtension.Remove(key))
            {
                throw new ArgumentException($"File type '{key}' is not registered.", nameof(extension));
            }
        }

        public bool TryGet(string extension, out string appId)
        {
            var key = NormalizeKey(extension);
            return appIdsByExtension.TryGetValue(key, out appId!);
        }

        public IReadOnlyList<string> FileTypes
        {
            get
            {
                var fileTypes = new List<string>(appIdsByExtension.Keys);
                fileTypes.Sort(StringComparer.Ordinal);
                return fileTypes;
            }
        }

        private static string NormalizeKey(string extension)
        {
            if (extension is null)
            {
                throw new ArgumentNullException(nameof(extension));
            }

            var withoutDot = extension.StartsWith(".", StringComparison.Ordinal)
                ? extension.Substring(1)
                : extension;
            if (string.IsNullOrWhiteSpace(withoutDot))
            {
                throw new ArgumentException("A file type extension must not be blank.", nameof(extension));
            }

            return withoutDot.ToLowerInvariant();
        }
    }
}
