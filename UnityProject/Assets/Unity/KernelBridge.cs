using System.Collections.Generic;
using System.Diagnostics;
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
    /// desktop with its window manager and taskbar, opens terminals through the manager,
    /// ticks the scheduler inside a frame budget and pumps every open terminal. A tick
    /// that overruns the budget skips exactly one frame to catch up.
    /// </summary>
    public sealed class KernelBridge : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset desktopTemplate;
        [SerializeField] private VisualTreeAsset windowTemplate;
        [SerializeField] private VisualTreeAsset terminalTemplate;
        [SerializeField] private int tickBudgetMilliseconds = 8;

        private readonly Stopwatch tickStopwatch = new Stopwatch();
        private readonly List<TerminalContent> terminals = new List<TerminalContent>();
        private VirtualFileSystem vfs;
        private Scheduler scheduler;
        private CommandRegistry commands;
        private WindowManager windowManager;
        private Taskbar taskbar;
        private bool baseCommandsInstalled;
        private bool skipNextTick;

        private void Start()
        {
            vfs = new VirtualFileSystem();
            scheduler = new Scheduler();
            commands = new CommandRegistry();

            var desktop = new Desktop(uiDocument.rootVisualElement, desktopTemplate);
            windowManager = new WindowManager(desktop.WindowLayer, windowTemplate);
            taskbar = new Taskbar(desktop.TaskbarRoot, windowManager);
            windowManager.WindowClosed += OnWindowClosed;
            taskbar.AddLauncher("terminal", OpenTerminalWindow);
            taskbar.AddLauncher("about", OpenAboutWindow);
            OpenTerminalWindow();
        }

        private void Update()
        {
            if (windowManager is null)
            {
                return;
            }

            TickWithinBudget();
            for (var terminalIndex = 0; terminalIndex < terminals.Count; terminalIndex++)
            {
                terminals[terminalIndex].Pump();
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

        private void OpenTerminalWindow()
        {
            var builtins = new BuiltinRegistry();
            var jobs = new JobTable();
            InstallCommandSet(builtins, jobs);
            var shellSession = new ShellSession("/", new Credentials(0));
            var terminalSession = new TerminalSession(scheduler, vfs, commands, builtins, shellSession, jobs);
            var terminal = new TerminalContent(terminalTemplate, terminalSession);
            terminals.Add(terminal);
            windowManager.Open(terminal);
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

        private void OnWindowClosed(Window window)
        {
            if (window.Content is TerminalContent terminal)
            {
                terminals.Remove(terminal);
            }
        }
    }
}
