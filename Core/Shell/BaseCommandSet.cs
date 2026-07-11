using System;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>The stock command and builtin set; mods register additional entries the same way.</summary>
    public static class BaseCommandSet
    {
        public static void Install(
            CommandRegistry commands,
            BuiltinRegistry builtins,
            VirtualFileSystem vfs,
            Scheduler scheduler,
            JobTable jobs)
        {
            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            InstallBuiltins(builtins, vfs, scheduler, jobs);

            commands.Register(new LsCommand(vfs));
            commands.Register(new CatCommand(vfs));
            commands.Register(new PwdCommand());
            commands.Register(new MkdirCommand(vfs));
            commands.Register(new RmCommand(vfs));
            commands.Register(new MvCommand(vfs));
            commands.Register(new CpCommand(vfs));
            commands.Register(new EchoCommand());
            commands.Register(new TouchCommand(vfs));
            commands.Register(new ClearCommand());
            commands.Register(new HelpCommand(commands, builtins));
            commands.Register(new PsCommand(scheduler));
            commands.Register(new KillCommand(scheduler));
        }

        public static void InstallBuiltins(
            BuiltinRegistry builtins,
            VirtualFileSystem vfs,
            Scheduler scheduler,
            JobTable jobs)
        {
            if (builtins is null)
            {
                throw new ArgumentNullException(nameof(builtins));
            }

            if (vfs is null)
            {
                throw new ArgumentNullException(nameof(vfs));
            }

            if (scheduler is null)
            {
                throw new ArgumentNullException(nameof(scheduler));
            }

            if (jobs is null)
            {
                throw new ArgumentNullException(nameof(jobs));
            }

            builtins.Register(new CdBuiltin(vfs));
            builtins.Register(new ExportBuiltin());
            builtins.Register(new SuBuiltin());
            builtins.Register(new WaitBuiltin(scheduler, jobs));
            builtins.Register(new JobsBuiltin(scheduler, jobs));
        }
    }
}
