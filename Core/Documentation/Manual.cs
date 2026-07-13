using System;
using System.Collections.Generic;

namespace Siegebox.Documentation
{
    /// <summary>
    /// The single authored catalog of manual pages keyed by command name. Registration rejects
    /// duplicates exactly like the command registry; lookups go through <see cref="TryGet"/>.
    /// </summary>
    public sealed class Manual
    {
        private readonly Dictionary<string, ManualPage> pages = new Dictionary<string, ManualPage>(StringComparer.Ordinal);

        public void Register(ManualPage page)
        {
            if (page is null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (pages.ContainsKey(page.Name))
            {
                throw new ArgumentException($"Manual page '{page.Name}' is already registered.", nameof(page));
            }

            pages.Add(page.Name, page);
        }

        public bool TryGet(string name, out ManualPage page)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return pages.TryGetValue(name, out page!);
        }
    }
}
