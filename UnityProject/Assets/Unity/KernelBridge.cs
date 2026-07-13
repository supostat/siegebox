using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Persistence;
using Siegebox.Process;
using Siegebox.Scripting;
using Siegebox.Security;
using Siegebox.Shell;
using Siegebox.Terminal;
using Siegebox.Vfs;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Siegebox.Unity
{
    /// <summary>
    /// The single MonoBehaviour and composition root. The scene-owned graph — desktop,
    /// window manager, app host and taskbar — is built once in Awake and persists across
    /// reboots. Every Boot rebuilds the kernel graph from scratch (events, vfs, scheduler,
    /// registries, mods, launchers) so a load starts from a clean kernel over the imported
    /// tree. Loading validates the save — version, tree, and window layout — BEFORE tearing
    /// down the live session, so a rejected save leaves the current game intact. Per-window
    /// rehydration runs after teardown; a window whose saved state is rejected there is
    /// dropped and logged rather than aborting the whole load.
    /// </summary>
    public sealed class KernelBridge : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset desktopTemplate;
        [SerializeField] private VisualTreeAsset windowTemplate;
        [SerializeField] private VisualTreeAsset terminalTemplate;
        [SerializeField] private VisualTreeAsset fileManagerTemplate;
        [SerializeField] private int tickBudgetMilliseconds = 8;

        private readonly Stopwatch tickStopwatch = new Stopwatch();

        private WindowManager windowManager;
        private AppHost appHost;
        private Taskbar taskbar;
        private SystemPanel systemPanel;

        private VirtualFileSystem vfs;
        private AuthenticationService authentication;
        private Scheduler scheduler;
        private CommandRegistry commands;
        private AppRegistry appRegistry;
        private bool skipNextTick;

        private void Awake()
        {
            var desktop = new Desktop(uiDocument.rootVisualElement, desktopTemplate);
            windowManager = new WindowManager(desktop.WindowLayer, windowTemplate);
            appHost = new AppHost(windowManager);
            taskbar = new Taskbar(desktop.TaskbarRoot, windowManager);
            var ownerIdentity = new WindowIdentity(UserSeed.PlayerName, UserSeed.PlayerUid, false);
            systemPanel = new SystemPanel(desktop.SystemPanelRoot, ownerIdentity);
        }

        private void Start() => Boot(null);

        private void Boot(SaveGame save)
        {
            var events = new EventBus((kernelEvent, handlerError) => Debug.LogException(handlerError));
            var loaded = save != null ? PrepareLoad(save, events) : null;

            windowManager.CloseAll();

            vfs = loaded?.Vfs ?? new VirtualFileSystem(events);
            authentication = new AuthenticationService(vfs);
            scheduler = new Scheduler(events: events);
            commands = new CommandRegistry();
            appRegistry = new AppRegistry();
            var fileTypes = new FileTypeRegistry();

            var scriptApi = new ScriptApi(commands, appRegistry, fileTypes, vfs, events, message => Debug.Log(message));
            var modLoader = new ModLoader(scriptApi);
            modLoader.RegisterNative(
                new ModManifest("base", "0.1.0", Array.Empty<string>(), 0, Array.Empty<string>()),
                () => InstallBaseMod(loaded != null));
            LoadDiskMods(modLoader);

            RebuildLaunchers();

            if (loaded == null)
            {
                LaunchApp("terminal");
            }
            else
            {
                RestoreWindows(loaded.Windows);
            }
        }

        private static LoadedSave PrepareLoad(SaveGame save, EventBus events)
        {
            var loaded = SaveSerializer.Load(save, events);
            ValidateWindowLayout(loaded.Windows);
            return loaded;
        }

        private static void ValidateWindowLayout(IReadOnlyList<WindowSnapshot> windows)
        {
            foreach (var snapshot in windows)
            {
                if (snapshot is null)
                {
                    throw new SaveFormatException("A saved window entry is null.");
                }

                if (string.IsNullOrEmpty(snapshot.AppId))
                {
                    throw new SaveFormatException("A saved window has no app id.");
                }

                if (snapshot.AppState != null)
                {
                    _ = SaveStore.Deserialize<object>(snapshot.AppState);
                }
            }
        }

        private static void LoadDiskMods(ModLoader modLoader)
        {
            foreach (var result in modLoader.LoadAll(ResolveModsRootPath()))
            {
                if (!result.Loaded)
                {
                    Debug.LogError($"Mod '{result.ModId}' failed to load: {result.Error}");
                }
            }
        }

        private void RebuildLaunchers()
        {
            taskbar.ClearLaunchers();
            foreach (var descriptor in appRegistry.Descriptors)
            {
                var launchDescriptor = descriptor;
                taskbar.AddLauncher(launchDescriptor.DisplayName, () => appHost.Launch(launchDescriptor));
            }

            taskbar.AddLauncher("Save", SaveGameNow);
            taskbar.AddLauncher("Load", LoadGameNow);
        }

        private void RestoreWindows(IReadOnlyList<WindowSnapshot> windows)
        {
            var ordered = new List<WindowSnapshot>(windows);
            ordered.Sort((left, right) => left.ZOrderIndex.CompareTo(right.ZOrderIndex));
            foreach (var snapshot in ordered)
            {
                if (!appRegistry.TryGet(snapshot.AppId, out var descriptor))
                {
                    continue;
                }

                try
                {
                    appHost.Rehydrate(descriptor, snapshot);
                }
                catch (Exception error)
                {
                    Debug.LogError($"Skipped a window while loading; its saved state was rejected. {error.Message}");
                }
            }
        }

        private void SaveGameNow() => SaveStore.Write(SaveSerializer.Capture(vfs.Export(), appHost.Capture()));

        private void LoadGameNow()
        {
            if (!SaveStore.TryRead(out var save))
            {
                Debug.LogWarning("No readable save to load; the live session is unchanged.");
                return;
            }

            try
            {
                Boot(save);
            }
            catch (Exception error) when (error is SaveFormatException || error is VfsException)
            {
                Debug.LogError($"Load rejected before teardown; the live session is unchanged. {error.Message}");
            }
            catch (Exception error)
            {
                Debug.LogError($"Load failed after teardown; the session may be incomplete. {error.Message}");
            }
        }

        private void Update()
        {
            if (scheduler is null)
            {
                return;
            }

            TickWithinBudget();
            var windows = windowManager.Windows;
            for (var windowIndex = 0; windowIndex < windows.Count; windowIndex++)
            {
                windows[windowIndex].Content.Pump();
            }
        }

        private void OnDestroy()
        {
            windowManager?.CloseAll();
        }

        private void TickWithinBudget()
        {
            if (skipNextTick)
            {
                skipNextTick = false;
                return;
            }

            tickStopwatch.Restart();
            scheduler.Tick();
            tickStopwatch.Stop();
            skipNextTick = tickStopwatch.ElapsedMilliseconds > tickBudgetMilliseconds;
        }

        private void InstallBaseMod(bool loadingSave)
        {
            if (!loadingSave)
            {
                UserSeed.Seed(vfs);
                BinSeed.Seed(vfs);
            }

            var bootBuiltins = new BuiltinRegistry();
            var bootJobs = new JobTable();
            BaseCommandSet.Install(commands, bootBuiltins, vfs, scheduler, bootJobs);
            commands.Register(new OpenCommand(appRegistry, appHost));
            appRegistry.Register(new AppDescriptor("terminal", "terminal", CreateTerminalApp));
            appRegistry.Register(new AppDescriptor("files", "files", CreateFileManagerApp));
            appRegistry.Register(new AppDescriptor("about", "about", CreateAboutApp));
        }

        private IApp CreateTerminalApp()
        {
            var builtins = new BuiltinRegistry();
            var jobs = new JobTable();
            BaseCommandSet.InstallBuiltins(builtins, vfs, scheduler, jobs);
            var shellSession = SessionLauncher.OpenFor(authentication, UserSeed.PlayerName);
            var terminalSession = new TerminalSession(scheduler, vfs, commands, builtins, shellSession, jobs);
            var identity = new WindowIdentity(UserSeed.PlayerName, shellSession.Credentials.Uid, shellSession.Credentials.IsRoot);
            return new TerminalContent(terminalTemplate, terminalSession, identity);
        }

        private IApp CreateFileManagerApp()
        {
            var session = SessionLauncher.OpenFor(authentication, UserSeed.PlayerName);
            var identity = new WindowIdentity(UserSeed.PlayerName, session.Credentials.Uid, session.Credentials.IsRoot);
            return new FileManagerApp(fileManagerTemplate, vfs, session.Credentials, identity);
        }

        private static IApp CreateAboutApp() => new StaticTextApp("about", "Siegebox — a desktop inside the game.");

        private void LaunchApp(string appId)
        {
            if (!appRegistry.TryGet(appId, out var descriptor))
            {
                throw new InvalidOperationException($"App '{appId}' is not registered.");
            }

            appHost.Launch(descriptor);
        }

        private static string ResolveModsRootPath()
        {
            var searchRoot = Application.isEditor
                ? Path.Combine(Application.dataPath, "..", "..")
                : Path.Combine(Application.dataPath, "..");
            return Path.GetFullPath(Path.Combine(searchRoot, "mods"));
        }
    }
}
