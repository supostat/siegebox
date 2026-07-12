using System;
using System.Collections.Generic;

namespace Siegebox.Security
{
    /// <summary>
    /// Parses /etc/shadow content — one <c>name:hash</c> entry per line, where hash is a
    /// <see cref="PasswordHash"/> string. This is the root-only half of the user db (the
    /// file is 0600); only the trusted <see cref="AuthenticationService"/> reads it. Blank
    /// lines and <c>#</c> comments are ignored; any other malformed line is rejected.
    /// </summary>
    public sealed class ShadowTable
    {
        private readonly Dictionary<string, string> hashesByName;

        private ShadowTable(Dictionary<string, string> hashesByName)
        {
            this.hashesByName = hashesByName;
        }

        public static ShadowTable Parse(string shadowContent)
        {
            if (shadowContent is null)
            {
                throw new ArgumentNullException(nameof(shadowContent));
            }

            var hashesByName = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var rawLine in shadowContent.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var separator = line.IndexOf(':');
                if (separator <= 0 || separator != line.LastIndexOf(':') || separator == line.Length - 1)
                {
                    throw new UserDatabaseException($"shadow: malformed entry '{line}'");
                }

                hashesByName[line.Substring(0, separator)] = line.Substring(separator + 1);
            }

            return new ShadowTable(hashesByName);
        }

        public bool TryGetHash(string name, out string hash) => hashesByName.TryGetValue(name, out hash!);
    }
}
