using System;
using System.Diagnostics;
using Siegebox.App;
using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Terminal;
using Siegebox.Vfs;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// The single MonoBehaviour and composition root: builds the kernel once, boots the
    /// desktop with its window manager and taskbar, registers every app in the registry,
    /// launches apps through the host, ticks the scheduler inside a frame budget and pumps
    /// every open window. A tick that overruns the budget skips exactly one frame to catch up.
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
        private bool baseCommandsInstalled;
        private bool skipNextTick;

        private void Start()
        {
            vfs = new VirtualFileSystem();
            scheduler = new Scheduler();
            commands = new CommandRegistry();
            appRegistry = new AppRegistry();

            var desktop = new Desktop(uiDocument.rootVisualElement, desktopTemplate);
            windowManager = new WindowManager(desktop.WindowLayer, windowTemplate);
            var taskbar = new Taskbar(desktop.TaskbarRoot, windowManager);
            appHost = new AppHost(windowManager);
            appRegistry.Register(new AppDescriptor("terminal", "terminal", CreateTerminalApp));
            appRegistry.Register(new AppDescriptor("files", "files", CreateFileManagerApp));
            commands.Register(new OpenCommand(appRegistry, appHost));
            foreach (var descriptor in appRegistry.Descriptors)
            {
                taskbar.AddLauncher(descriptor.DisplayName, () => appHost.Launch(descriptor));
            }

            taskbar.AddLauncher("about", OpenAboutWindow);
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

        private IApp CreateTerminalApp()
        {
            var builtins = new BuiltinRegistry();
            var jobs = new JobTable();
            InstallCommandSet(builtins, jobs);
            var shellSession = new ShellSession("/", new Credentials(0));
            var terminalSession = new TerminalSession(scheduler, vfs, commands, builtins, shellSession, jobs);
            return new TerminalContent(terminalTemplate, terminalSession);
        }

        private IApp CreateFileManagerApp() => new FileManagerApp(fileManagerTemplate, vfs, new Credentials(0));

        private void LaunchApp(string appId)
        {
            if (!appRegistry.TryGet(appId, out var descriptor))
            {
                throw new InvalidOperationException($"App '{appId}' is not registered.");
            }

            appHost.Launch(descriptor);
        }

        private void OpenAboutWindow()
        {
            windowManager.Open(new PlaceholderContent("about", "Siegebox — a desktop inside the game."));
        }

        private void InstallCommandSet(BuiltinRegistry builtins, JobTable jobs)
        {
            if (baseCommandsInstalled)
            {
                BaseCommandSet.InstallBuiltins(builtins, vfs, scheduler, jobs);
                return;
            }

            BaseCommandSet.Install(commands, builtins, vfs, scheduler, jobs);
            baseCommandsInstalled = true;
        }
    }
}
