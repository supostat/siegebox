using System;
using System.Collections.Generic;
using System.Text;

namespace Siegebox.Security
{
    /// <summary>
    /// Parses and serializes /etc/shadow content — one <c>name:hash</c> entry per line, where
    /// hash is a <see cref="PasswordHash"/> string. This is the root-only half of the user db
    /// (the file is 0600); only the trusted <see cref="AuthenticationService"/> reads it and the
    /// passwd command rewrites it. Entry order is preserved across a Parse/Render round-trip so
    /// a rewrite touches only the changed entry. Blank lines and <c>#</c> comments are ignored;
    /// any other malformed line is rejected.
    /// </summary>
    public sealed class ShadowTable
    {
        private readonly List<string> orderedNames;
        private readonly Dictionary<string, string> hashesByName;

        private ShadowTable(List<string> orderedNames, Dictionary<string, string> hashesByName)
        {
            this.orderedNames = orderedNames;
            this.hashesByName = hashesByName;
        }

        public static ShadowTable Parse(string shadowContent)
        {
            if (shadowContent is null)
            {
                throw new ArgumentNullException(nameof(shadowContent));
            }

            var orderedNames = new List<string>();
            var hashesByName = new Dictionary<string, string>(StringComparer.Ordinal);
            var lineNumber = 0;
            foreach (var rawLine in shadowContent.Split('\n'))
            {
                lineNumber++;
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                var separator = line.IndexOf(':');
                if (separator <= 0 || separator != line.LastIndexOf(':') || separator == line.Length - 1)
                {
                    throw new UserDatabaseException($"shadow: malformed entry on line {lineNumber}");
                }

                var name = line.Substring(0, separator);
                if (!hashesByName.ContainsKey(name))
                {
                    orderedNames.Add(name);
                }

                hashesByName[name] = line.Substring(separator + 1);
            }

            return new ShadowTable(orderedNames, hashesByName);
        }

        public bool TryGetHash(string name, out string hash) => hashesByName.TryGetValue(name, out hash!);

        public void SetHash(string name, string hash)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (hash is null)
            {
                throw new ArgumentNullException(nameof(hash));
            }

            RequireShadowField(name, nameof(name));
            RequireShadowField(hash, nameof(hash));

            if (!hashesByName.ContainsKey(name))
            {
                orderedNames.Add(name);
            }

            hashesByName[name] = hash;
        }

        private static void RequireShadowField(string value, string parameterName)
        {
            foreach (var character in value)
            {
                if (character == ':' || char.IsControl(character))
                {
                    throw new ArgumentException($"A shadow {parameterName} must not contain ':' or control characters.", parameterName);
                }
            }
        }

        public string Render()
        {
            var builder = new StringBuilder();
            foreach (var name in orderedNames)
            {
                builder.Append(name).Append(':').Append(hashesByName[name]).Append('\n');
            }

            return builder.ToString();
        }
    }
}
