using NUnit.Framework;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Shell.Tests;

namespace Siegebox.Scripting.Tests
{
    /// <summary>
    /// Pins the phase contract that scripted content is indistinguishable from C# content
    /// on every shell path: redirects, conditional lists, background jobs with kill, the
    /// help listing, and app launch through the open command.
    /// </summary>
    [TestFixture]
    public sealed class LuaShellParityTests
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
        public void Output_redirect_writes_lua_stdout_to_a_file()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('greet', function(ctx) ctx.write('hello\\n') return 0 end)",
                "greet");

            harness.Run("greet > /out.txt");

            Assert.That(harness.ReadFile("/out.txt"), Is.EqualTo("hello\n"));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Lua_producer_larger_than_the_pipe_capacity_delivers_everything_through_backpressure()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('flood', function(ctx)" +
                " local chunk = string.rep('x', 4096)" +
                " for i = 1, 20 do ctx.write(chunk) end" +
                " return 0 end)" +
                " siegebox.register_command('count', function(ctx)" +
                " local total = 0" +
                " while true do" +
                "   local chunk = ctx.read()" +
                "   if chunk == nil then break end" +
                "   total = total + #chunk" +
                " end" +
                " ctx.write(tostring(total))" +
                " return 0 end)",
                "backpressure");

            harness.Run("flood | count");

            Assert.That(harness.DrainOutput(), Is.EqualTo("81920"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.DrainError(), Is.EqualTo(""));
        }

        [Test]
        public void Lua_exit_codes_drive_conditional_lists()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('ok', function(ctx) return 0 end)" +
                " siegebox.register_command('fail', function(ctx) return 1 end)",
                "conditionals");

            harness.Run("ok && echo yes");
            Assert.That(harness.DrainOutput(), Is.EqualTo("yes\n"));

            harness.Run("fail && echo never || echo rescued");
            Assert.That(harness.DrainOutput(), Is.EqualTo("rescued\n"));
        }

        [Test]
        public void Background_lua_command_sleeping_on_read_is_killed_cleanly_with_130()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk(
                "siegebox.register_command('stall', function(ctx) ctx.read() return 0 end)",
                "stall");

            harness.Run("stall &", maxTicks: 8);
            var stallPid = ShellHarness.AnnouncedPid(harness.DrainError());
            Assert.That(harness.Scheduler.Contains(stallPid), Is.True);

            harness.Run($"kill {stallPid}");

            Assert.That(harness.Scheduler.Contains(stallPid), Is.False);
            Assert.That(harness.Scheduler.TryPeekExitCode(stallPid, out var exitCode), Is.True);
            Assert.That(exitCode, Is.EqualTo(Scheduler.InterruptExitCode));
            harness.Run("echo alive");
            Assert.That(harness.DrainOutput(), Is.EqualTo("alive\n"));
        }

        [Test]
        public void Help_lists_a_lua_command_alongside_the_stock_commands()
        {
            var (harness, host) = CreateLuaShell();
            host.RunChunk("siegebox.register_command('zulu', function(ctx) return 0 end)", "zulu");

            harness.Run("help");

            var listing = harness.DrainOutput();
            Assert.That(listing, Does.Contain("zulu"));
            Assert.That(listing, Does.Contain("echo"));
        }

        [Test]
        public void Open_launches_a_lua_registered_app_through_the_same_registry_path()
        {
            var harness = new ShellHarness();
            var apps = new AppRegistry();
            var api = new ScriptApi(
                harness.Commands,
                apps,
                new FileTypeRegistry(),
                harness.Vfs,
                new EventBus(),
                _ => { });
            var host = new LuaHost();
            api.InstallInto(host, api.CreateScope());
            var launcher = new RecordingAppLauncher();
            harness.RegisterCommand(new OpenCommand(apps, launcher));
            host.RunChunk(
                "siegebox.register_app{ id = 'notes', on_launch = function(app) app.set_text('up') end }",
                "notes");

            harness.Run("open notes");

            Assert.That(launcher.Launched, Has.Count.EqualTo(1));
            Assert.That(launcher.Launched[0].Id, Is.EqualTo("notes"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.DrainError(), Is.EqualTo(""));
        }
    }
}
