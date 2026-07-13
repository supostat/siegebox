using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class CommandsTests
    {
        private static readonly Credentials Root = new Credentials(0);

        [Test]
        public void Mkdir_touch_ls_round_trip_lists_sorted_names()
        {
            var harness = new ShellHarness();

            harness.Run("mkdir /d ; touch /d/b /d/a ; ls /d");

            Assert.That(harness.DrainOutput(), Is.EqualTo("a\nb\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Cat_streams_a_file()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "file-data");

            harness.Run("cat /f");

            Assert.That(harness.DrainOutput(), Is.EqualTo("file-data"));
        }

        [Test]
        public void Cat_without_arguments_relays_stdin()
        {
            var harness = new ShellHarness();
            var bytes = Encoding.UTF8.GetBytes("piped-data");
            harness.TerminalInput.Write(bytes, 0, bytes.Length);
            harness.TerminalInput.CloseWrite();

            harness.Run("cat");

            Assert.That(harness.DrainOutput(), Is.EqualTo("piped-data"));
        }

        [Test]
        public void Pwd_prints_the_working_directory()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/d", new PermissionMode(0b111_101_101), Root);

            harness.Run("pwd");
            Assert.That(harness.DrainOutput(), Is.EqualTo("/\n"));

            harness.Run("cd /d ; pwd");
            Assert.That(harness.DrainOutput(), Is.EqualTo("/d\n"));
        }

        [Test]
        public void Mv_moves_and_cp_copies()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/src", "content");

            harness.Run("mv /src /dst ; cp /dst /copy");

            Assert.That(harness.ReadFile("/dst"), Is.EqualTo("content"));
            Assert.That(harness.ReadFile("/copy"), Is.EqualTo("content"));
            Assert.That(harness.Vfs.List("/", Root), Does.Not.Contain("src"));
        }

        [Test]
        public void Rm_removes_a_file()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "x");

            harness.Run("rm /f");

            Assert.That(harness.Vfs.List("/", Root), Does.Not.Contain("f"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Echo_joins_arguments_with_spaces()
        {
            var harness = new ShellHarness();

            harness.Run("echo a b");

            Assert.That(harness.DrainOutput(), Is.EqualTo("a b\n"));
        }

        [Test]
        public void Clear_emits_the_escape_sequence()
        {
            var harness = new ShellHarness();

            harness.Run("clear");

            var output = harness.DrainOutput();
            Assert.That(output, Is.EqualTo(ClearCommand.ClearSequence));
            Assert.That(output, Is.EqualTo((char)0x1B + "[2J" + (char)0x1B + "[H"));
        }

        [Test]
        public void Help_lists_builtins_and_commands_sorted()
        {
            var harness = new ShellHarness();

            harness.Run("help");

            var output = harness.DrainOutput();
            Assert.That(output, Does.Contain("builtins: cd export jobs su wait\n"));
            Assert.That(output, Does.Contain("commands: cat chmod clear cp echo help kill ls man mkdir mv passwd ps pwd rm touch\n"));
        }

        [Test]
        public void Ps_shows_a_spinning_background_process()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());

            harness.Run("spin & ps", maxTicks: 8);

            var output = harness.DrainOutput();
            Assert.That(output, Does.Contain("Running spin\n"));
            Assert.That(output, Does.Contain("Running ps\n"));
            Assert.That(output, Does.Contain("Sleeping sh\n"));
        }

        [Test]
        public void Kill_leaves_a_collectible_interrupt_status()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());

            harness.Run($"kill {spinPid}");

            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(130));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Rm_on_a_non_empty_directory_reports_the_error()
        {
            var harness = new ShellHarness();

            harness.Run("mkdir /d ; touch /d/f ; rm /d");

            Assert.That(harness.DrainError(), Does.Contain("rm: /d: Directory not empty"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Ls_without_read_permission_reports_the_error_and_continues()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.Vfs.CreateDirectory("/secret", new PermissionMode(0b111_000_000), Root);
            harness.Vfs.CreateDirectory("/open", new PermissionMode(0b111_101_101), Root);

            harness.Run("ls /secret /open");

            Assert.That(harness.DrainError(), Does.Contain("ls: /secret: Permission denied"));
            Assert.That(harness.DrainOutput(), Does.Contain("/open:"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Cp_onto_an_existing_destination_reports_the_error()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/a", "x");
            harness.WriteFile("/b", "y");

            harness.Run("cp /a /b");

            Assert.That(harness.DrainError(), Does.Contain("cp: /b: File exists"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Touch_on_an_existing_file_is_a_no_op()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "keep");

            harness.Run("touch /f");

            Assert.That(harness.ReadFile("/f"), Is.EqualTo("keep"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Relative_paths_resolve_against_the_session_cwd()
        {
            var harness = new ShellHarness();

            harness.Run("mkdir /d ; cd /d ; touch rel ; ls");

            Assert.That(harness.DrainOutput(), Is.EqualTo("rel\n"));
            Assert.That(harness.Vfs.Stat("/d/rel", Root).Type, Is.EqualTo(NodeType.File));
        }

        [Test]
        public void Kill_rejects_a_non_numeric_and_an_unknown_pid()
        {
            var harness = new ShellHarness();

            harness.Run("kill notapid");
            Assert.That(harness.DrainError(), Does.Contain("kill: notapid: arguments must be process ids"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));

            harness.Run("kill 9999");
            Assert.That(harness.DrainError(), Does.Contain("kill: 9999: No such process"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Mv_with_one_argument_prints_usage()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/only", "x");

            harness.Run("mv /only");

            Assert.That(harness.DrainError(), Does.Contain("mv: usage"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Broken_command_lines_never_throw_out_of_tick()
        {
            var harness = new ShellHarness();

            Assert.That(() => harness.Run("cat /missing | nosuchcmd | cat > /out ; ls /nowhere"), Throws.Nothing);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Kill_of_its_own_pid_drains_and_yields_the_interrupt_status()
        {
            var harness = new ShellHarness();
            var warmupExecutorPid = harness.Run("echo warmup");
            harness.DrainOutput();
            var nextExecutorPid = warmupExecutorPid + 2;
            var ownKillPid = nextExecutorPid + 1;

            Assert.That(() => harness.Run($"kill {ownKillPid}"), Throws.Nothing);

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(130));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
            Assert.That(harness.Scheduler.TryPeekExitCode(ownKillPid, out _), Is.False);
        }

        [Test]
        public void Kill_of_the_shells_own_executor_drains_and_the_shell_recovers()
        {
            var harness = new ShellHarness();
            var warmupExecutorPid = harness.Run("echo warmup");
            harness.DrainOutput();
            var nextExecutorPid = warmupExecutorPid + 2;

            Assert.That(() => harness.Run($"kill {nextExecutorPid}"), Throws.Nothing);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));

            harness.Run("echo ok");
            Assert.That(harness.DrainOutput(), Is.EqualTo("ok\n"));
            Assert.That(harness.Scheduler.TryPeekExitCode(nextExecutorPid, out _), Is.False);
        }

        [Test]
        public void Mv_and_cp_resolve_both_relative_arguments_against_the_cwd()
        {
            var harness = new ShellHarness();

            harness.Run("mkdir /d ; cd /d ; touch a ; mv a b ; cp b c ; ls");

            Assert.That(harness.DrainOutput(), Is.EqualTo("b\nc\n"));
            Assert.That(harness.Vfs.Stat("/d/b", Root).Type, Is.EqualTo(NodeType.File));
            Assert.That(harness.Vfs.Stat("/d/c", Root).Type, Is.EqualTo(NodeType.File));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Ls_prints_file_and_symlink_arguments_as_themselves()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "x");
            harness.Vfs.CreateDirectory("/d", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateFile("/d/child", new PermissionMode(0b110_100_100), Root);
            harness.Vfs.CreateSymlink("/link", "/d", Root);

            harness.Run("ls /f");
            Assert.That(harness.DrainOutput(), Is.EqualTo("/f\n"));

            harness.Run("ls /link");
            Assert.That(harness.DrainOutput(), Is.EqualTo("/link\n"));

            harness.Run("ls /f /d");
            Assert.That(harness.DrainOutput(), Is.EqualTo("/f:\n/f\n\n/d:\nchild\n"));
        }

        [Test]
        public void Cat_continues_after_an_open_failure()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/present", "data");

            harness.Run("cat /missing /present");

            Assert.That(harness.DrainOutput(), Is.EqualTo("data"));
            Assert.That(harness.DrainError(), Does.Contain("cat: /missing: No such file or directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Non_root_cannot_kill_a_root_owned_process()
        {
            var harness = new ShellHarness();
            harness.SeedUsers();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());
            harness.Run("su player");

            harness.Run($"kill {spinPid}");

            Assert.That(harness.DrainError(), Does.Contain($"kill: ({spinPid}) - Operation not permitted"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.Scheduler.Contains(spinPid), Is.True);
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out _), Is.False);
        }

        [Test]
        public void Root_kills_a_foreign_uid_process()
        {
            var harness = new ShellHarness();
            harness.SeedUsers();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("su player");
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());
            harness.FeedInput("root\n");
            harness.Run("su");

            harness.Run($"kill {spinPid}");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(130));
        }

        [Test]
        public void Non_root_kills_its_own_process()
        {
            var harness = new ShellHarness();
            harness.SeedUsers();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("su player");
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());

            harness.Run($"kill {spinPid}");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(130));
        }

        [Test]
        public void Kill_with_a_mix_of_invalid_and_valid_pids_kills_the_valid_ones()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());

            harness.Run($"kill notapid {spinPid}");

            Assert.That(harness.DrainError(), Does.Contain("kill: notapid: arguments must be process ids"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.Scheduler.TryPeekExitCode(spinPid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(130));
        }

        [Test]
        public void Cat_pushes_a_large_payload_through_pipe_backpressure()
        {
            var harness = new ShellHarness();
            var reader = new ByteReaderCommand("readall");
            harness.RegisterCommand(reader);
            var payload = new StringBuilder(100_000);
            for (var index = 0; index < 100_000; index++)
            {
                payload.Append((char)('a' + index % 26));
            }

            harness.WriteFile("/big", payload.ToString());

            harness.Run("cat /big | readall");

            Assert.That(reader.LastProcess.Received, Is.EqualTo(Encoding.UTF8.GetBytes(payload.ToString())));
            Assert.That(reader.LastProcess.SawEof, Is.True);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Buffered_command_output_larger_than_the_pipe_drains_under_backpressure()
        {
            var harness = new ShellHarness();
            var reader = new ByteReaderCommand("readall");
            harness.RegisterCommand(reader);
            var argument = new string('a', 70_000);

            harness.Run("echo " + argument + " | readall");

            Assert.That(reader.LastProcess.Received, Is.EqualTo(Encoding.UTF8.GetBytes(argument + "\n")));
            Assert.That(reader.LastProcess.SawEof, Is.True);
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Cat_exits_with_one_on_a_broken_pipe()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", new PermissionMode(0b110_100_100), Root);
            var content = vfs.OpenForWrite("/f", WriteBehavior.Truncate, new PermissionMode(0b110_100_100), Root);
            var bytes = Encoding.UTF8.GetBytes("doomed");
            content.Write(bytes, 0, bytes.Length);
            var closedStdout = new PipeStream();
            closedStdout.CloseRead();
            var descriptors = new FileDescriptorTable(new PipeStream(), closedStdout, new PipeStream());
            var context = new ExecutionContext("/", Root, new Dictionary<string, string>(), descriptors);
            var scheduler = new Scheduler();

            var pid = scheduler.Spawn(new CatCommand(vfs).CreateProcess(context, new[] { "/f" }), "cat");
            for (var tick = 0; tick < 16 && scheduler.ProcessCount > 0; tick++)
            {
                scheduler.Tick();
            }

            Assert.That(scheduler.TryCollectExitCode(pid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(1));
        }
    }
}
