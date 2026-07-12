using System;
using System.Diagnostics;
using System.IO;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Process;
using Siegebox.Scripting;
using Siegebox.Shell;
using Siegebox.Terminal;
using Siegebox.Vfs;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace Siegebox.Unity
{
    /// <summary>
    /// The single MonoBehaviour and composition root: builds the kernel with its event bus,
    /// installs base content as the first (native) mod, loads disk mods, builds the taskbar
    /// from the app registry, ticks the scheduler inside a frame budget and pumps every open
    /// window. A tick that overruns the budget skips exactly one frame to catch up.
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
        private VirtualFileSystem vfs;
        private Scheduler scheduler;
        private CommandRegistry commands;
        private AppRegistry appRegistry;
        private WindowManager windowManager;
        private AppHost appHost;
        private bool skipNextTick;

        private void Start()
        {
            var events = new EventBus((kernelEvent, handlerError) => Debug.LogException(handlerError));
            vfs = new VirtualFileSystem(events);
            scheduler = new Scheduler(events: events);
            commands = new CommandRegistry();
            appRegistry = new AppRegistry();
            var fileTypes = new FileTypeRegistry();

            var desktop = new Desktop(uiDocument.rootVisualElement, desktopTemplate);
            windowManager = new WindowManager(desktop.WindowLayer, windowTemplate);
            var taskbar = new Taskbar(desktop.TaskbarRoot, windowManager);
            appHost = new AppHost(windowManager);

            var scriptApi = new ScriptApi(commands, appRegistry, fileTypes, vfs, events, message => Debug.Log(message));
            var modLoader = new ModLoader(scriptApi);
            modLoader.RegisterNative(
                new ModManifest("base", "0.1.0", Array.Empty<string>(), 0, Array.Empty<string>()),
                InstallBaseMod);
            foreach (var result in modLoader.LoadAll(ResolveModsRootPath()))
            {
                if (!result.Loaded)
                {
                    Debug.LogError($"Mod '{result.ModId}' failed to load: {result.Error}");
                }
            }

            foreach (var descriptor in appRegistry.Descriptors)
            {
                taskbar.AddLauncher(descriptor.DisplayName, () => appHost.Launch(descriptor));
            }

            LaunchApp("terminal");
        }

        private void Update()
        {
            if (windowManager is null)
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

        private void InstallBaseMod()
        {
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
            var shellSession = new ShellSession("/", new Credentials(0));
            var terminalSession = new TerminalSession(scheduler, vfs, commands, builtins, shellSession, jobs);
            return new TerminalContent(terminalTemplate, terminalSession);
        }

        private IApp CreateFileManagerApp() => new FileManagerApp(fileManagerTemplate, vfs, new Credentials(0));

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
