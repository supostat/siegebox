using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MoonSharp.Interpreter;
using MoonSharp.Interpreter.Serialization.Json;

namespace Siegebox.Scripting
{
    /// <summary>
    /// Validated mod metadata. Validation is a security boundary: ids match an allowlist
    /// pattern and script entries must be bare file names, so a manifest can never point
    /// outside its own mod directory. Violations raise <see cref="ModLoadException"/>
    /// naming the offending field.
    /// </summary>
    public sealed class ModManifest
    {
        public ModManifest(string id, string version, IReadOnlyList<string> dependencies, int loadOrder, IReadOnlyList<string> scripts)
        {
            if (id is null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (version is null)
            {
                throw new ArgumentNullException(nameof(version));
            }

            if (dependencies is null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            if (scripts is null)
            {
                throw new ArgumentNullException(nameof(scripts));
            }

            RequireId(id, "id");
            if (string.IsNullOrWhiteSpace(version))
            {
                throw new ModLoadException("manifest field 'version' must not be blank");
            }

            foreach (var dependency in dependencies)
            {
                RequireId(dependency, "dependencies");
            }

            foreach (var script in scripts)
            {
                RequireBareFileName(script);
            }

            Id = id;
            Version = version;
            Dependencies = new ReadOnlyCollection<string>(new List<string>(dependencies));
            LoadOrder = loadOrder;
            Scripts = new ReadOnlyCollection<string>(new List<string>(scripts));
        }

        public string Id { get; }

        public string Version { get; }

        public IReadOnlyList<string> Dependencies { get; }

        public int LoadOrder { get; }

        public IReadOnlyList<string> Scripts { get; }

        public static ModManifest Parse(string json)
        {
            if (json is null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var manifest = ParseJsonObject(json);
            return new ModManifest(
                RequiredStringField(manifest, "id"),
                RequiredStringField(manifest, "version"),
                OptionalStringArrayField(manifest, "dependencies"),
                OptionalIntegerField(manifest, "loadOrder"),
                OptionalStringArrayField(manifest, "scripts"));
        }

        private static Table ParseJsonObject(string json)
        {
            DynValue parsed;
            try
            {
                parsed = JsonTableConverter.ParseString(json);
            }
            catch (SyntaxErrorException)
            {
                throw new ModLoadException("manifest is not valid json");
            }

            if (parsed.Type != DataType.Table)
            {
                throw new ModLoadException("manifest is not a json object");
            }

            return parsed.Table;
        }

        private static string RequiredStringField(Table manifest, string fieldName)
        {
            var value = manifest.Get(fieldName);
            if (value.Type != DataType.String)
            {
                throw new ModLoadException($"manifest field '{fieldName}' must be a string");
            }

            return value.String;
        }

        private static IReadOnlyList<string> OptionalStringArrayField(Table manifest, string fieldName)
        {
            var value = manifest.Get(fieldName);
            if (value.IsNil())
            {
                return Array.Empty<string>();
            }

            if (value.Type != DataType.Table)
            {
                throw new ModLoadException($"manifest field '{fieldName}' must be an array of strings");
            }

            var items = new List<string>();
            var array = value.Table;
            for (var index = 1; index <= array.Length; index++)
            {
                var item = array.Get(index);
                if (item.Type != DataType.String)
                {
                    throw new ModLoadException($"manifest field '{fieldName}' must be an array of strings");
                }

                items.Add(item.String);
            }

            return items;
        }

        private static int OptionalIntegerField(Table manifest, string fieldName)
        {
            var value = manifest.Get(fieldName);
            if (value.IsNil())
            {
                return 0;
            }

            if (value.Type != DataType.Number
                || value.Number != Math.Floor(value.Number)
                || value.Number < int.MinValue
                || value.Number > int.MaxValue)
            {
                throw new ModLoadException($"manifest field '{fieldName}' must be an integer");
            }

            return (int)value.Number;
        }

        private static void RequireId(string value, string fieldName)
        {
            if (!ModIdentifier.IsValid(value))
            {
                throw new ModLoadException($"manifest field '{fieldName}' must match {ModIdentifier.Rule}");
            }
        }

        private static void RequireBareFileName(string script)
        {
            if (string.IsNullOrWhiteSpace(script)
                || script.IndexOf('/') >= 0
                || script.IndexOf('\\') >= 0
                || script.Contains("..")
                || System.IO.Path.IsPathRooted(script))
            {
                throw new ModLoadException($"manifest field 'scripts' must contain bare file names, got '{script}'");
            }
        }
    }
}
