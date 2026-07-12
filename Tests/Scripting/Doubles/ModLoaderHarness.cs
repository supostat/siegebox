using System;
using System.Collections.Generic;
using System.IO;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;
using Siegebox.Shell.Tests;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    /// <summary>
    /// Mod-loading stack over a throwaway disk root: fresh registries, a ScriptApi and a
    /// ModLoader with the native 'base' mod (installing 'nativecmd') pre-registered.
    /// WriteMod lays out a disk mod; Dispose deletes the root.
    /// </summary>
    internal sealed class ModLoaderHarness : IDisposable
    {
        public ModLoaderHarness()
        {
            ModsRoot = Path.Combine(Path.GetTempPath(), "siegebox-mods-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(ModsRoot);
            Commands = new CommandRegistry();
            Apps = new AppRegistry();
            FileTypes = new FileTypeRegistry();
            var bus = new EventBus();
            Api = new ScriptApi(Commands, Apps, FileTypes, new VirtualFileSystem(bus), bus, _ => { });
            Loader = new ModLoader(Api);
            Loader.RegisterNative(
                new ModManifest("base", "0.1.0", Array.Empty<string>(), 0, Array.Empty<string>()),
                () => Commands.Register(new ProbeCommand("nativecmd")));
        }

        public string ModsRoot { get; }

        public CommandRegistry Commands { get; }

        public AppRegistry Apps { get; }

        public FileTypeRegistry FileTypes { get; }

        public ScriptApi Api { get; }

        public ModLoader Loader { get; }

        public IReadOnlyList<ModLoadResult> LoadAll() => Loader.LoadAll(ModsRoot);

        public void WriteMod(string directoryName, string manifestJson, params (string Name, string Code)[] scripts)
        {
            var modDirectory = Path.Combine(ModsRoot, directoryName);
            Directory.CreateDirectory(modDirectory);
            File.WriteAllText(Path.Combine(modDirectory, "manifest.json"), manifestJson);
            foreach (var (name, code) in scripts)
            {
                File.WriteAllText(Path.Combine(modDirectory, name), code);
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(ModsRoot))
            {
                Directory.Delete(ModsRoot, true);
            }
        }
    }
}
