using System;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// The host-facing entry point: parses a command line and spawns a scheduler-ticked
    /// executor for it. Terminal streams are wrapped once so process death never closes
    /// the terminal underneath the session.
    /// </summary>
    public sealed class Shell
    {
        private readonly Scheduler scheduler;
        private readonly ShellSession session;
        private readonly JobTable jobs;
        private readonly IByteStream terminalInput;
        private readonly IByteStream terminalOutput;
        private readonly IByteStream terminalError;
        private readonly CommandLineLexer lexer = new CommandLineLexer();
        private readonly CommandLineParser parser = new CommandLineParser();
        private readonly PipelineAssembler assembler;
        private int activeExecutorPid;

        public Shell(
            Scheduler scheduler,
            VirtualFileSystem vfs,
            CommandRegistry commands,
            BuiltinRegistry builtins,
            ShellSession session,
            JobTable jobs,
            IByteStream terminalInput,
            IByteStream terminalOutput,
            IByteStream terminalError)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            if (vfs is null)
            {
                throw new ArgumentNullException(nameof(vfs));
            }

            if (commands is null)
            {
                throw new ArgumentNullException(nameof(commands));
            }

            if (builtins is null)
            {
                throw new ArgumentNullException(nameof(builtins));
            }

            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
            if (terminalInput is null)
            {
                throw new ArgumentNullException(nameof(terminalInput));
            }

            if (terminalOutput is null)
            {
                throw new ArgumentNullException(nameof(terminalOutput));
            }

            if (terminalError is null)
            {
                throw new ArgumentNullException(nameof(terminalError));
            }

            this.terminalInput = new NonClosingStream(terminalInput);
            this.terminalOutput = new NonClosingStream(terminalOutput);
            this.terminalError = new NonClosingStream(terminalError);
            assembler = new PipelineAssembler(scheduler, vfs, commands, builtins, new ArgumentExpander());
        }

        public ShellSession Session => session;

        public JobTable Jobs => jobs;

        /// <summary>
        /// Parses and launches one command line; returns the executor pid, or 0 for a blank
        /// line. The scheduler must be ticked for anything to actually run. Only one command
        /// line may be in flight per shell.
        /// </summary>
        public int Execute(string line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (activeExecutorPid > 0)
            {
                if (scheduler.Contains(activeExecutorPid))
                {
                    throw new InvalidOperationException("The previous command line is still running.");
                }

                scheduler.TryCollectExitCode(activeExecutorPid, out _);
                activeExecutorPid = 0;
            }

            try
            {
                var tokens = lexer.Tokenize(line);
                if (tokens.Count == 0)
                {
                    return 0;
                }

                var list = parser.Parse(tokens);
                var executor = new ListExecutorProcess(list, session, scheduler, assembler, jobs, CreateOwnContext());
                activeExecutorPid = scheduler.Spawn(executor, "sh");
            }
            catch (ShellParseException error)
            {
                session.LastExitCode = 2;
                var message = new MessageProcess(CreateOwnContext(), $"sh: {error.Message}\n", FileDescriptorTable.Stderr, 2);
                activeExecutorPid = scheduler.Spawn(message, "sh");
            }

            return activeExecutorPid;
        }

        private ExecutionContext CreateOwnContext()
            => session.CreateContext(new FileDescriptorTable(terminalInput, terminalOutput, terminalError));
    }
}
