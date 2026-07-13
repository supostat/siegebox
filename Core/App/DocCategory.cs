using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Siegebox.App
{
    /// <summary>A named group of doc-browser entries, e.g. "file system" or "process".</summary>
    public sealed class DocCategory
    {
        public DocCategory(string name, IReadOnlyList<DocEntry> entries)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            if (entries is null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            Entries = new ReadOnlyCollection<DocEntry>(new List<DocEntry>(entries));
        }

        public string Name { get; }

        public IReadOnlyList<DocEntry> Entries { get; }
    }
}
