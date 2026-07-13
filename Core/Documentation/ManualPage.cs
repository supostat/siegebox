using System;

namespace Siegebox.Documentation
{
    /// <summary>
    /// One authored manual entry: the terse synopsis shown by <c>--help</c> and the full
    /// description body. Every field is required and non-blank so the catalog can never
    /// surface an empty page.
    /// </summary>
    public sealed class ManualPage
    {
        public ManualPage(string name, string category, string synopsis, string description)
        {
            Name = Required(name, nameof(name));
            Category = Required(category, nameof(category));
            Synopsis = Required(synopsis, nameof(synopsis));
            Description = Required(description, nameof(description));
        }

        public string Name { get; }

        public string Category { get; }

        public string Synopsis { get; }

        public string Description { get; }

        private static string Required(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"A manual page {field} must not be blank.", field);
            }

            return value;
        }
    }
}
