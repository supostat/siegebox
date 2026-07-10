using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Terminal;
using Siegebox.Vfs;

namespace Siegebox.Terminal.Tests
{
    /// <summary>Full terminal stack: vfs + scheduler + stock registries + TerminalSession.</summary>
    internal sealed class TerminalHarness
    {
        public TerminalHarness(string workingDirectory = "/", int uid = 0)
        {
            Vfs = new VirtualFileSystem();
            Scheduler = new Scheduler();
            Commands = new CommandRegistry();
            Builtins = new BuiltinRegistry();
            Jobs = new JobTable();
            BaseCommandSet.Install(Commands, Builtins, Vfs, Scheduler, Jobs);
            ShellSession = new ShellSession(workingDirectory, new Credentials(uid));
            Session = new TerminalSession(Scheduler, Vfs, Commands, Builtins, ShellSession, Jobs);
        }

        public VirtualFileSystem Vfs { get; }

        public Scheduler Scheduler { get; }

        public CommandRegistry Commands { get; }

        public BuiltinRegistry Builtins { get; }

        public JobTable Jobs { get; }

        public ShellSession ShellSession { get; }

        public TerminalSession Session { get; }

        public void RegisterCommand(ICommand command) => Commands.Register(command);

        public void TickAndPump(int count = 8)
        {
            for (var tick = 0; tick < count; tick++)
            {
                Scheduler.Tick();
                Session.Pump();
            }
        }

        public bool RunLine(string line, int ticks = 8)
        {
            var accepted = Session.SubmitLine(line);
            TickAndPump(ticks);
            return accepted;
        }
    }
}
