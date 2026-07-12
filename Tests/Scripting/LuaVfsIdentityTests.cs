using System;
using System.IO;
using NUnit.Framework;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell.Tests;
using Siegebox.Vfs;

namespace Siegebox.Scripting.Tests
{
    /// <summary>
    /// Pins that a scripted command's vfs is mediated under the calling process's own
    /// identity, exactly like a C# command — an unprivileged command gets EACCES on
    /// root-only files — while the privileged install-identity vfs a mod loads with does
    /// not survive into any handler, not even through a reference stashed during load.
    /// </summary>
    [TestFixture]
    public sealed class LuaVfsIdentityTests
    {
        private const int UnprivilegedUid = 1000;
        private static readonly Credentials Root = new Credentials(0);

        private static (ShellHarness Harness, LuaHost Host, LuaVfsGate Gate) Build(int uid)
        {
            var harness = new ShellHarness(uid: uid);
            var api = new ScriptApi(
                harness.Commands,
                new AppRegistry(),
                new FileTypeRegistry(),
                harness.Vfs,
                new EventBus(),
                _ => { });
            var host = new LuaHost();
            var gate = api.InstallInto(host, api.CreateScope());
            return (harness, host, gate);
        }

        private static void SeedFileAsRoot(VirtualFileSystem vfs, string path, int mode, string content)
        {
            var stream = vfs.OpenForWrite(path, WriteBehavior.Truncate, new PermissionMode(mode), Root);
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
        }

        [Test]
        public void LuaCommand_vfs_read_denied_for_unprivileged_user()
        {
            var (harness, host, _) = Build(UnprivilegedUid);
            SeedFileAsRoot(harness.Vfs, "/secret.txt", 0b110_000_000, "topsecret");
            host.RunChunk(
                "siegebox.register_command('peek', function(ctx) return ctx.write(ctx.vfs.read('/secret.txt')) end)",
                "peek");

            harness.Run("peek");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.DrainError(), Does.Contain("EACCES"));
        }

        [Test]
        public void LuaCommand_vfs_write_denied_for_unprivileged_user()
        {
            var (harness, host, _) = Build(UnprivilegedUid);
            harness.Vfs.CreateDirectory("/vault", new PermissionMode(0b111_000_000), Root);
            host.RunChunk(
                "siegebox.register_command('plant', function(ctx) ctx.vfs.write('/vault/backdoor.txt', 'pwned') return 0 end)",
                "plant");

            harness.Run("plant");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainError(), Does.Contain("EACCES"));
            Assert.That(harness.Vfs.List("/vault", Root), Is.Empty);
        }

        [Test]
        public void LuaCommand_vfs_allowed_under_own_identity()
        {
            var (harness, host, _) = Build(UnprivilegedUid);
            harness.Vfs.CreateDirectory("/home", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.Chown("/home", UnprivilegedUid, UnprivilegedUid, Root);
            host.RunChunk(
                "siegebox.register_command('own', function(ctx)" +
                " ctx.vfs.write('/home/mine.txt', 'hi')" +
                " return ctx.write(ctx.vfs.read('/home/mine.txt')) end)",
                "own");

            harness.Run("own");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.DrainOutput(), Is.EqualTo("hi"));
            Assert.That(harness.DrainError(), Is.EqualTo(""));
        }

        [Test]
        public void Mod_load_may_seed_files_as_install_identity()
        {
            var (harness, host, _) = Build(UnprivilegedUid);

            host.RunChunk("siegebox.vfs.write('/seeded.txt', 'seedcontent')", "seed");

            Assert.That(harness.ReadFile("/seeded.txt"), Is.EqualTo("seedcontent"));
        }

        [Test]
        public void Global_vfs_is_not_reachable_from_a_command_handler()
        {
            var (harness, host, gate) = Build(UnprivilegedUid);
            SeedFileAsRoot(harness.Vfs, "/secret.txt", 0b110_000_000, "topsecret");
            host.RunChunk(
                "siegebox.register_command('leak', function(ctx) return ctx.write(siegebox.vfs.read('/secret.txt')) end)",
                "leak");
            gate.Close();

            harness.Run("leak");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.DrainError(), Does.Contain("mod load"));
        }

        [Test]
        public void Stashed_load_vfs_reference_is_dead_after_load()
        {
            var (harness, host, gate) = Build(UnprivilegedUid);
            SeedFileAsRoot(harness.Vfs, "/secret.txt", 0b110_000_000, "topsecret");
            host.RunChunk(
                "local stashed = siegebox.vfs" +
                " siegebox.register_command('sneak', function(ctx) return ctx.write(stashed.read('/secret.txt')) end)",
                "sneak");
            gate.Close();

            harness.Run("sneak");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainOutput(), Does.Not.Contain("topsecret"));
            Assert.That(harness.DrainError(), Does.Contain("mod load"));
        }

        [Test]
        public void Ctx_vfs_stashed_by_a_privileged_command_cannot_be_reused_after_dropping_privilege()
        {
            var (harness, host, _) = Build(0);
            SeedFileAsRoot(harness.Vfs, "/secret.txt", 0b110_000_000, "topsecret");
            host.RunChunk(
                "local captured" +
                " siegebox.register_command('cache', function(ctx) captured = ctx.vfs return 0 end)" +
                " siegebox.register_command('use', function(ctx) return ctx.write(captured.read('/secret.txt')) end)",
                "confuseddeputy");

            harness.Run("cache");
            harness.Run("su 1000");
            harness.DrainOutput();
            harness.DrainError();

            harness.Run("use");

            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.DrainOutput(), Does.Not.Contain("topsecret"));
            Assert.That(harness.DrainError(), Does.Contain("only available while the command runs"));
        }

        [Test]
        public void A_disk_mod_loaded_through_the_loader_cannot_reach_root_vfs_at_runtime()
        {
            var harness = new ShellHarness(uid: UnprivilegedUid);
            SeedFileAsRoot(harness.Vfs, "/secret.txt", 0b110_000_000, "topsecret");
            var api = new ScriptApi(
                harness.Commands, new AppRegistry(), new FileTypeRegistry(), harness.Vfs, new EventBus(), _ => { });
            var loader = new ModLoader(api);
            var modsRoot = Path.Combine(Path.GetTempPath(), "siegebox-vfs-" + Guid.NewGuid().ToString("N"));
            var modDirectory = Path.Combine(modsRoot, "prowler");
            Directory.CreateDirectory(modDirectory);
            try
            {
                File.WriteAllText(
                    Path.Combine(modDirectory, "manifest.json"),
                    "{\"id\": \"prowler\", \"version\": \"1\", \"scripts\": [\"p.lua\"]}");
                File.WriteAllText(
                    Path.Combine(modDirectory, "p.lua"),
                    "siegebox.register_command('prowl', function(ctx) return ctx.write(siegebox.vfs.read('/secret.txt')) end)");

                var results = loader.LoadAll(modsRoot);

                Assert.That(results, Has.Some.Matches<ModLoadResult>(result => result.ModId == "prowler" && result.Loaded));
                harness.Run("prowl");
                Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
                Assert.That(harness.DrainOutput(), Does.Not.Contain("topsecret"));
                Assert.That(harness.DrainError(), Does.Contain("mod load"));
            }
            finally
            {
                Directory.Delete(modsRoot, true);
            }
        }
    }
}
