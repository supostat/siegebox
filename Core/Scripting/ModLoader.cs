using System;
using System.Collections.Generic;
using System.IO;
using MoonSharp.Interpreter;

namespace Siegebox.Scripting
{
    /// <summary>
    /// Loads mods at boot: native in-code installers first, in registration order, then
    /// disk mods from the mods root ordered by (LoadOrder, Id). A broken disk mod is
    /// rolled back and reported without stopping the others; a failing native mod fails
    /// fast — base content must never half-install. A missing mods root means zero
    /// external mods, not an error.
    /// </summary>
    public sealed class ModLoader
    {
        public const int MaxManifestBytes = 65_536;
        public const int MaxScriptBytes = 1_048_576;

        private const string ManifestFileName = "manifest.json";

        private readonly ScriptApi api;
        private readonly List<NativeMod> nativeMods = new List<NativeMod>();
        private readonly List<string> loadedModIds = new List<string>();
        private bool loaded;

        public ModLoader(ScriptApi api)
        {
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public IReadOnlyList<string> LoadedModIds => loadedModIds;

        public void RegisterNative(ModManifest manifest, Action install)
        {
            if (manifest is null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (install is null)
            {
                throw new ArgumentNullException(nameof(install));
            }

            foreach (var native in nativeMods)
            {
                if (native.Manifest.Id == manifest.Id)
                {
                    throw new ArgumentException($"Native mod '{manifest.Id}' is already registered.", nameof(manifest));
                }
            }

            nativeMods.Add(new NativeMod(manifest, install));
        }

        public IReadOnlyList<ModLoadResult> LoadAll(string modsRootPath)
        {
            if (modsRootPath is null)
            {
                throw new ArgumentNullException(nameof(modsRootPath));
            }

            if (loaded)
            {
                throw new InvalidOperationException("Mods are already loaded.");
            }

            loaded = true;
            var results = new List<ModLoadResult>();
            foreach (var native in nativeMods)
            {
                native.Install();
                loadedModIds.Add(native.Manifest.Id);
                results.Add(new ModLoadResult(native.Manifest.Id, true, string.Empty));
            }

            if (!Directory.Exists(modsRootPath))
            {
                return results;
            }

            var candidates = CollectCandidates(modsRootPath, results);
            candidates.Sort(CompareLoadOrder);
            foreach (var candidate in candidates)
            {
                results.Add(LoadDiskMod(candidate));
            }

            return results;
        }

        private List<DiskMod> CollectCandidates(string modsRootPath, List<ModLoadResult> results)
        {
            var seenIds = new HashSet<string>(loadedModIds, StringComparer.Ordinal);
            var candidates = new List<DiskMod>();
            var directories = Directory.GetDirectories(modsRootPath);
            Array.Sort(directories, StringComparer.Ordinal);
            foreach (var directory in directories)
            {
                try
                {
                    var manifest = ReadManifest(directory);
                    if (!seenIds.Add(manifest.Id))
                    {
                        throw new ModLoadException($"mod id '{manifest.Id}' is already declared");
                    }

                    candidates.Add(new DiskMod(directory, manifest));
                }
                catch (Exception manifestError)
                {
                    results.Add(new ModLoadResult(Path.GetFileName(directory), false, manifestError.Message));
                }
            }

            return candidates;
        }

        private ModLoadResult LoadDiskMod(DiskMod mod)
        {
            foreach (var dependency in mod.Manifest.Dependencies)
            {
                if (!loadedModIds.Contains(dependency))
                {
                    return new ModLoadResult(mod.Manifest.Id, false, $"dependency '{dependency}' is not loaded");
                }
            }

            var scope = api.CreateScope();
            try
            {
                var host = new LuaHost();
                api.InstallInto(host, scope);
                foreach (var scriptName in mod.Manifest.Scripts)
                {
                    host.RunChunk(ReadScript(mod.Directory, scriptName), $"{mod.Manifest.Id}/{scriptName}");
                }

                loadedModIds.Add(mod.Manifest.Id);
                return new ModLoadResult(mod.Manifest.Id, true, string.Empty);
            }
            catch (Exception loadError)
            {
                return new ModLoadResult(mod.Manifest.Id, false, RollBackAndDescribe(scope, loadError));
            }
        }

        private static string RollBackAndDescribe(ModRegistrationScope scope, Exception loadError)
        {
            try
            {
                scope.Rollback();
                return MessageOf(loadError);
            }
            catch (Exception rollbackError)
            {
                return $"{MessageOf(loadError)} (rollback also failed: {rollbackError.Message})";
            }
        }

        private static ModManifest ReadManifest(string modDirectory)
        {
            var manifestPath = Path.Combine(modDirectory, ManifestFileName);
            if (!File.Exists(manifestPath))
            {
                throw new ModLoadException("manifest.json is missing");
            }

            if (new FileInfo(manifestPath).Length > MaxManifestBytes)
            {
                throw new ModLoadException($"manifest.json exceeds {MaxManifestBytes} bytes");
            }

            return ModManifest.Parse(
                ReadWithinNestingLimit(manifestPath, "manifest.json", NestingDepthScanner.ManifestExceedsMaxDepth));
        }

        private static string ReadScript(string modDirectory, string scriptName)
        {
            var scriptPath = Path.Combine(modDirectory, scriptName);
            if (!File.Exists(scriptPath))
            {
                throw new ModLoadException($"script '{scriptName}' is missing");
            }

            if (new FileInfo(scriptPath).Length > MaxScriptBytes)
            {
                throw new ModLoadException($"script '{scriptName}' exceeds {MaxScriptBytes} bytes");
            }

            return ReadWithinNestingLimit(scriptPath, $"script '{scriptName}'", NestingDepthScanner.ScriptExceedsMaxDepth);
        }

        private static string ReadWithinNestingLimit(string path, string displayName, Func<string, bool> exceedsMaxDepth)
        {
            var text = File.ReadAllText(path);
            if (exceedsMaxDepth(text))
            {
                throw new ModLoadException($"{displayName} exceeds a nesting depth of {NestingDepthScanner.MaxDepth}");
            }

            return text;
        }

        private static int CompareLoadOrder(DiskMod left, DiskMod right)
        {
            var byLoadOrder = left.Manifest.LoadOrder.CompareTo(right.Manifest.LoadOrder);
            return byLoadOrder != 0 ? byLoadOrder : StringComparer.Ordinal.Compare(left.Manifest.Id, right.Manifest.Id);
        }

        private static string MessageOf(Exception loadError)
            => loadError is InterpreterException interpreterError
                ? interpreterError.DecoratedMessage ?? interpreterError.Message
                : loadError.Message;

        private sealed class NativeMod
        {
            public NativeMod(ModManifest manifest, Action install)
            {
                Manifest = manifest;
                Install = install;
            }

            public ModManifest Manifest { get; }

            public Action Install { get; }
        }

        private sealed class DiskMod
        {
            public DiskMod(string directory, ModManifest manifest)
            {
                Directory = directory;
                Manifest = manifest;
            }

            public string Directory { get; }

            public ModManifest Manifest { get; }
        }
    }
}
