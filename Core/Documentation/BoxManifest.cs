using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Serialization.Json;

namespace Siegebox.Documentation
{
    /// <summary>
    /// The per-box authored metadata read from <c>/etc/siegebox/box.json</c>: the scenario target
    /// name and the doc-browser hints. Both fields are optional and forward-compatible (unknown
    /// fields are ignored), but a field that is present with the wrong type — including an explicit
    /// JSON <c>null</c> — is rejected, because authored content that malformed is a bug, not input.
    /// </summary>
    public sealed class BoxManifest
    {
        public BoxManifest(string target, IReadOnlyList<string> hints)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            if (hints is null)
            {
                throw new ArgumentNullException(nameof(hints));
            }

            Hints = new ReadOnlyCollection<string>(new List<string>(hints));
        }

        public string Target { get; }

        public IReadOnlyList<string> Hints { get; }

        public static BoxManifest Parse(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var root = ParseRoot(json);
            return new BoxManifest(ReadTarget(root), ReadHints(root));
        }

        private static Table ParseRoot(string json)
        {
            DynValue parsed;
            try
            {
                parsed = JsonTableConverter.ParseString(json);
            }
            catch (SyntaxErrorException)
            {
                throw new BoxManifestException("box manifest is not valid json");
            }

            if (parsed.Type != DataType.Table)
            {
                throw new BoxManifestException("box manifest is not a json object");
            }

            return parsed.Table;
        }

        private static string ReadTarget(Table root)
        {
            var value = root.Get("target");
            if (value.Type == DataType.Nil)
            {
                return "";
            }

            if (value.Type != DataType.String)
            {
                throw new BoxManifestException("box manifest field 'target' must be a string");
            }

            return value.String;
        }

        private static IReadOnlyList<string> ReadHints(Table root)
        {
            var value = root.Get("hints");
            if (value.Type == DataType.Nil)
            {
                return Array.Empty<string>();
            }

            if (value.Type != DataType.Table)
            {
                throw new BoxManifestException("box manifest field 'hints' must be an array of strings");
            }

            var hints = new List<string>();
            var array = value.Table;
            for (var index = 1; index <= array.Length; index++)
            {
                var hintValue = array.Get(index);
                if (hintValue.Type != DataType.String)
                {
                    throw new BoxManifestException("box manifest field 'hints' must be an array of strings");
                }

                hints.Add(hintValue.String);
            }

            return hints;
        }
    }
}
