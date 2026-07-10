using System.Text;
using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Full shell stack on pipes: vfs + scheduler + stock registries + terminal PipeStreams.
    /// Run(line) executes one command line to completion; Drain* return decoded terminal bytes.
    /// </summary>
    internal sealed class ShellHarness
    {
        public const int DefaultMaxTicks = 128;

        public ShellHarness(string workingDirectory = "/", int uid = 0)
        {
            Vfs = new VirtualFileSystem();
            Scheduler = new Scheduler();
            Commands = new CommandRegistry();
            Builtins = new BuiltinRegistry();
            BaseCommandSet.Install(Commands, Builtins, Vfs, Scheduler, Jobs);
            Session = new ShellSession(workingDirectory, new Credentials(uid));
            TerminalInput = new PipeStream();
            TerminalOutput = new PipeStream();
            TerminalError = new PipeStream();
            Shell = new Shell(
                Scheduler, Vfs, Commands, Builtins, Session, Jobs,
                TerminalInput, TerminalOutput, TerminalError);
        }

        public VirtualFileSystem Vfs { get; }

        public Scheduler Scheduler { get; }

        public CommandRegistry Commands { get; }

        public BuiltinRegistry Builtins { get; }

        public JobTable Jobs { get; } = new JobTable();

        public ShellSession Session { get; }

        public PipeStream TerminalInput { get; }

        public PipeStream TerminalOutput { get; }

        public PipeStream TerminalError { get; }

        public Shell Shell { get; }

        public void RegisterCommand(ICommand command) => Commands.Register(command);

        public int Run(string line, int maxTicks = DefaultMaxTicks)
        {
            var pid = Shell.Execute(line);
            RunUntilIdle(maxTicks);
            return pid;
        }

        public static int AnnouncedPid(string announcement)
        {
            var parts = announcement.Trim().Split(' ');
            return int.Parse(parts[1]);
        }

        public void RunUntilIdle(int maxTicks = DefaultMaxTicks)
        {
            for (var tick = 0; tick < maxTicks && Scheduler.ProcessCount > 0; tick++)
            {
                Scheduler.Tick();
            }
        }

        public void Tick(int count = 1)
        {
            for (var tick = 0; tick < count; tick++)
            {
                Scheduler.Tick();
            }
        }

        public string DrainOutput() => DrainToString(TerminalOutput);

        public string DrainError() => DrainToString(TerminalError);

        public string ReadFile(string path)
        {
            var stream = Vfs.Open(path, OpenMode.Read, new Credentials(0));
            return DrainToString(stream);
        }

        public void WriteFile(string path, string content)
        {
            var root = new Credentials(0);
            var stream = Vfs.OpenForWrite(path, WriteBehavior.Truncate, new PermissionMode(0b110_100_100), root);
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        private static string DrainToString(IByteStream stream)
        {
            var text = new StringBuilder();
            var chunk = new byte[256];
            while (true)
            {
                var result = stream.Read(chunk, 0, chunk.Length);
                if (result.Status != StreamStatus.Ok)
                {
                    return text.ToString();
                }

                text.Append(Encoding.UTF8.GetString(chunk, 0, result.Count));
            }
        }
    }
}
