using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class ScriptApiTests
    {
        private sealed class ApiEnvironment
        {
            public ApiEnvironment()
            {
                Commands = new CommandRegistry();
                Apps = new AppRegistry();
                FileTypes = new FileTypeRegistry();
                Bus = new EventBus();
                Vfs = new VirtualFileSystem(Bus);
                Log = new List<string>();
                Api = new ScriptApi(Commands, Apps, FileTypes, Vfs, Bus, Log.Add);
                Host = new LuaHost();
                Scope = Api.CreateScope();
                Api.InstallInto(Host, Scope);
            }

            public CommandRegistry Commands { get; }

            public AppRegistry Apps { get; }

            public FileTypeRegistry FileTypes { get; }

            public EventBus Bus { get; }

            public VirtualFileSystem Vfs { get; }

            public List<string> Log { get; }

            public ScriptApi Api { get; }

            public LuaHost Host { get; }

            public ModRegistrationScope Scope { get; }
        }

        [Test]
        public void Vfs_write_then_read_round_trips()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "siegebox.vfs.write('/note.txt', 'ahoy') return siegebox.vfs.read('/note.txt')",
                "roundtrip");

            Assert.That(result.String, Is.EqualTo("ahoy"));
        }

        [Test]
        public void Vfs_list_returns_sorted_names()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "siegebox.vfs.write('/b.txt', 'b') siegebox.vfs.write('/a.txt', 'a')" +
                " return table.concat(siegebox.vfs.list('/'), ',')",
                "list");

            Assert.That(result.String, Is.EqualTo("a.txt,b.txt,dev"));
        }

        [Test]
        public void Registered_file_type_is_visible_in_the_registry()
        {
            var environment = new ApiEnvironment();

            environment.Host.RunChunk("siegebox.register_file_type('TXT', 'editor')", "filetype");

            Assert.That(environment.FileTypes.TryGet("txt", out var appId), Is.True);
            Assert.That(appId, Is.EqualTo("editor"));
        }

        [Test]
        public void Log_reaches_the_sink()
        {
            var environment = new ApiEnvironment();

            environment.Host.RunChunk("siegebox.log('hello sink')", "log");

            Assert.That(environment.Log, Is.EqualTo(new[] { "hello sink" }));
        }

        [Test]
        public void File_type_override_is_restored_by_rollback()
        {
            var environment = new ApiEnvironment();
            environment.FileTypes.Register("txt", "base-editor");

            environment.Host.RunChunk("siegebox.register_file_type('txt', 'mod-editor')", "override");
            Assert.That(environment.FileTypes.TryGet("txt", out var overridden), Is.True);
            Assert.That(overridden, Is.EqualTo("mod-editor"));

            environment.Scope.Rollback();

            Assert.That(environment.FileTypes.TryGet("txt", out var restored), Is.True);
            Assert.That(restored, Is.EqualTo("base-editor"));
        }

        [Test]
        public void Rollback_removes_commands_apps_file_types_and_subscriptions()
        {
            var environment = new ApiEnvironment();
            environment.Host.RunChunk(
                "siegebox.register_command('modcmd', function(ctx) return 0 end)" +
                " siegebox.register_app{ id = 'modapp', on_launch = function(app) end }" +
                " siegebox.register_file_type('lua', 'modapp')" +
                " siegebox.subscribe('file.opened', function(evt) siegebox.log('seen') end)",
                "fullmod");

            environment.Scope.Rollback();

            Assert.That(environment.Commands.TryGet("modcmd", out _), Is.False);
            Assert.That(environment.Apps.TryGet("modapp", out _), Is.False);
            Assert.That(environment.FileTypes.TryGet("lua", out _), Is.False);
            environment.Bus.Publish(KernelEvent.FileOpened("/note.txt", 0, "read"));
            Assert.That(environment.Log, Is.Empty);
            Assert.That(() => environment.Scope.Rollback(), Throws.Nothing);
        }

        [Test]
        public void Rollback_undoes_two_overrides_of_one_extension_in_reverse_order()
        {
            var environment = new ApiEnvironment();

            environment.Host.RunChunk(
                "siegebox.register_file_type('txt', 'a') siegebox.register_file_type('txt', 'b')",
                "twice");
            Assert.That(environment.FileTypes.TryGet("txt", out var latest), Is.True);
            Assert.That(latest, Is.EqualTo("b"));

            environment.Scope.Rollback();

            Assert.That(environment.FileTypes.TryGet("txt", out _), Is.False);
        }

        [Test]
        public void Bad_argument_types_raise_pcall_able_errors()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "local ok, err = pcall(function() siegebox.register_command(123, function() end) end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "badargs");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("register_command"));
        }

        [Test]
        public void Invalid_command_name_raises_a_pcall_able_error()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "local ok, err = pcall(function() siegebox.register_command('Bad Name!', function() end) end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "badname");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("name"));
            Assert.That(environment.Commands.TryGet("Bad Name!", out _), Is.False);
        }

        [Test]
        public void Vfs_read_of_a_missing_file_is_a_catchable_ENOENT()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "local ok, err = pcall(function() return siegebox.vfs.read('/absent') end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "enoent");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("ENOENT"));
            Assert.That(result.String, Does.Contain("/absent"));
        }

        [Test]
        public void Siegebox_exposes_exactly_the_documented_keys()
        {
            var environment = new ApiEnvironment();

            var siegeboxKeys = environment.Host.RunChunk(
                "local keys = {} for key in pairs(siegebox) do keys[#keys + 1] = key end" +
                " table.sort(keys) return table.concat(keys, ',')",
                "keys");
            var vfsKeys = environment.Host.RunChunk(
                "local keys = {} for key in pairs(siegebox.vfs) do keys[#keys + 1] = key end" +
                " table.sort(keys) return table.concat(keys, ',')",
                "vfskeys");

            Assert.That(
                siegeboxKeys.String,
                Is.EqualTo("log,register_app,register_command,register_file_type,subscribe,vfs"));
            Assert.That(vfsKeys.String, Is.EqualTo("list,read,write"));
        }

        [Test]
        public void Failed_file_type_registration_leaves_no_phantom_rollback_entry()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "local ok, err = pcall(function() siegebox.register_file_type('txt', ' ') end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "blankappid");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("register_file_type"));
            Assert.That(environment.FileTypes.TryGet("txt", out _), Is.False);
            Assert.That(() => environment.Scope.Rollback(), Throws.Nothing);
        }

        [Test]
        public void Duplicate_command_registration_raises_a_pcall_able_error()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "siegebox.register_command('hello', function(ctx) return 0 end)" +
                " local ok, err = pcall(function() siegebox.register_command('hello', function(ctx) return 0 end) end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "dupcommand");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("register_command"));
            Assert.That(result.String, Does.Contain("hello"));
        }

        [Test]
        public void Duplicate_app_registration_raises_a_pcall_able_error()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "siegebox.register_app{ id = 'twinapp', on_launch = function(app) end }" +
                " local ok, err = pcall(function()" +
                "   siegebox.register_app{ id = 'twinapp', on_launch = function(app) end }" +
                " end)" +
                " return tostring(ok) .. '|' .. tostring(err)",
                "dupapp");

            Assert.That(result.String, Does.StartWith("false|"));
            Assert.That(result.String, Does.Contain("register_app"));
            Assert.That(result.String, Does.Contain("twinapp"));
        }

        [Test]
        public void Every_api_callback_rejects_wrong_argument_types_as_pcall_able_errors()
        {
            var environment = new ApiEnvironment();

            var result = environment.Host.RunChunk(
                "local attempts = {" +
                " function() siegebox.log({}) end," +
                " function() siegebox.register_command({}, function() end) end," +
                " function() siegebox.register_file_type({}, 'editor') end," +
                " function() siegebox.subscribe('file.opened', 'not a function') end," +
                " function() siegebox.register_app(false) end," +
                " function() siegebox.register_app{ id = 'noapp' } end," +
                " function() siegebox.vfs.read({}) end," +
                " function() siegebox.vfs.write('/p.txt', {}) end," +
                " function() siegebox.vfs.list(false) end }" +
                " local rejected = 0" +
                " for _, attempt in ipairs(attempts) do" +
                "   if pcall(attempt) == false then rejected = rejected + 1 end" +
                " end" +
                " return rejected .. '/' .. #attempts",
                "argsweep");

            Assert.That(result.String, Is.EqualTo("9/9"));
        }
    }
}
