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
    /// The single MonoBehaviour: builds the kernel once, ticks it inside a frame budget and
    /// pumps the terminal controller. A tick that overruns the budget skips exactly one frame
    /// to catch up; the deterministic step/probe fuses stay inside the scheduler.
    /// </summary>
    public sealed class KernelBridge : MonoBehaviour
    {
        [SerializeField] private UIDocument uiDocument;
        [SerializeField] private VisualTreeAsset terminalTemplate;
        [SerializeField] private int tickBudgetMilliseconds = 8;

        private readonly Stopwatch tickStopwatch = new Stopwatch();
        private Scheduler scheduler;
        private TerminalController controller;
        private bool skipNextTick;

        private void Awake()
        {
            var vfs = new VirtualFileSystem();
            scheduler = new Scheduler();
            var commands = new CommandRegistry();
            var builtins = new BuiltinRegistry();
            var jobs = new JobTable();
            BaseCommandSet.Install(commands, builtins, vfs, scheduler, jobs);
            var shellSession = new ShellSession("/", new Credentials(0));
            var terminalSession = new TerminalSession(scheduler, vfs, commands, builtins, shellSession, jobs);

            var window = new TerminalWindow(uiDocument.rootVisualElement, terminalTemplate);
            var view = new TerminalView(window.Root);
            controller = new TerminalController(terminalSession, window, view);
        }

        private void Update()
        {
            TickWithinBudget();
            controller.Pump();
        }

        private void OnDestroy()
        {
            controller?.Dispose();
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
    }
}
