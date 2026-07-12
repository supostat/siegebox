using MoonSharp.Interpreter;
using NUnit.Framework;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;
using Siegebox.Shell.Tests;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class LuaCommandTests
    {
        private static (ShellHarness Harness, LuaHost Host) CreateLuaShell()
        {
            var harness = new ShellHarness();
            var api = new ScriptApi(
                harness.Commands,
                new AppRegistry(),
                new FileTypeRegistry(),
                harness.Vfs,
                new EventBus(),
                _ => { });
            var host = new LuaHost();
            api.InstallInto(host, api.CreateScope());
            return (harness, host);
        }

        [Test]
        public void Piped_input_is_read_transformed_and_written()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('upper', function(ctx)" +
                " while true do" +
                "   local chunk = ctx.read()" +
                "   if chunk == nil then break end" +
                "   ctx.write(string.upper(chunk))" +
                " end" +
                " return 0 end)",
                "upper");

            harness.Run("echo hi | upper");

            Assert.That(harness.DrainOutput(), Is.EqualTo("HI\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Arguments_reach_the_handler_context()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('argsdump', function(ctx)" +
                " ctx.write(table.concat(ctx.args, ','))" +
                " return 0 end)",
                "argsdump");

            harness.Run("argsdump alpha beta gamma");

            Assert.That(harness.DrainOutput(), Is.EqualTo("alpha,beta,gamma"));
        }

        [Test]
        public void Numeric_return_becomes_the_exit_code()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk("siegebox.register_command('exit3', function(ctx) return 3 end)", "exit3");

            harness.Run("exit3");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(3));
        }

        [Test]
        public void Command_that_never_reads_completes()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('greet', function(ctx) ctx.write('hello, world!\\n') return 0 end)",
                "greet");

            harness.Run("greet");

            Assert.That(harness.DrainOutput(), Is.EqualTo("hello, world!\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Write_err_reaches_stderr_and_leaves_stdout_empty()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('warn', function(ctx) ctx.write_err('careful!\\n') return 0 end)",
                "warn");

            harness.Run("warn");

            Assert.That(harness.DrainError(), Is.EqualTo("careful!\n"));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Handler_without_a_return_statement_exits_zero_with_empty_stderr()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk("siegebox.register_command('quiet', function(ctx) ctx.write('done') end)", "quiet");

            harness.Run("quiet");

            Assert.That(harness.DrainOutput(), Is.EqualTo("done"));
            Assert.That(harness.DrainError(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Multibyte_character_straddling_the_read_chunk_boundary_survives_intact()
        {
            var (harness, host) = CreateLuaShell();
            var straddling = new string('a', 4095) + "я";
            host.RunChunk(
                "local expected = '" + straddling + "'\n" +
                "siegebox.register_command('mbcheck', function(ctx)" +
                " local total = ''" +
                " while true do" +
                "   local chunk = ctx.read()" +
                "   if chunk == nil then break end" +
                "   total = total .. chunk" +
                " end" +
                " if total == expected then ctx.write('intact') else ctx.write('mangled') end" +
                " return 0 end)",
                "mbcheck");
            harness.WriteFile("/straddle.txt", straddling);

            harness.Run("mbcheck < /straddle.txt");

            Assert.That(harness.DrainOutput(), Is.EqualTo("intact"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Read_past_end_of_stream_keeps_returning_nil()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('eofprobe', function(ctx)" +
                " local first = ctx.read()" +
                " local second = ctx.read()" +
                " if first == nil and second == nil then ctx.write('both nil') end" +
                " return 0 end)",
                "eofprobe");
            harness.WriteFile("/empty", "");

            harness.Run("eofprobe < /empty");

            Assert.That(harness.DrainOutput(), Is.EqualTo("both nil"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Handler_error_fails_with_a_lua_error_line_on_stderr()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk("siegebox.register_command('boom', function(ctx) error('kaputt') end)", "boom");

            harness.Run("boom");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            var error = harness.DrainError();
            Assert.That(error, Does.Contain("boom: lua error:"));
            Assert.That(error, Does.Contain("kaputt"));
        }

        [Test]
        public void Infinite_loop_exhausts_the_budget_and_the_scheduler_survives()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk("siegebox.register_command('spinlua', function(ctx) while true do end end)", "spinlua");

            harness.Run("spinlua");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("spinlua: instruction budget exceeded"));

            harness.Run("echo recovered");
            Assert.That(harness.DrainOutput(), Is.EqualTo("recovered\n"));
        }

        [Test]
        public void Output_flood_aborts_at_the_cap()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('flood', function(ctx)" +
                " local chunk = string.rep('x', 65536)" +
                " while true do ctx.write(chunk) end end)",
                "flood");

            harness.Run("flood");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("flood: output limit exceeded"));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
        }

        [Test]
        public void Non_numeric_return_fails_with_exit_1_and_a_stderr_line()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk("siegebox.register_command('badexit', function(ctx) return 'nope' end)", "badexit");

            harness.Run("badexit");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("badexit"));
        }

        [Test]
        public void Stashed_read_capability_cannot_yield_outside_a_command()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('stash', function(ctx) stolen_read = ctx.read return 0 end)",
                "stash");
            harness.Run("stash");
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));

            var error = Assert.Throws<ScriptRuntimeException>(() => host.RunChunk("stolen_read()", "stolen"));

            Assert.That(error.Message, Does.Contain("yielded outside a command context"));
        }
    }
}
