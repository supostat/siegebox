using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class BuiltinRegistry
    {
        private readonly Dictionary<string, IBuiltin> builtins = new Dictionary<string, IBuiltin>(StringComparer.Ordinal);

        public void Register(IBuiltin builtin)
        {
            if (builtin is null)
            {
                throw new ArgumentNullException(nameof(builtin));
            }

            if (builtins.ContainsKey(builtin.Name))
            {
                throw new ArgumentException($"Builtin '{builtin.Name}' is already registered.", nameof(builtin));
            }

            builtins.Add(builtin.Name, builtin);
        }

        public bool TryGet(string name, out IBuiltin builtin)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return builtins.TryGetValue(name, out builtin!);
        }

        public IReadOnlyList<string> Names
        {
            get
            {
                var names = new List<string>(builtins.Keys);
                names.Sort(StringComparer.Ordinal);
                return names;
            }
        }
    }
}
