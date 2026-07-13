using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins that elevation is a property of the executable FILE: a uid-1000 process spawned for
    /// a setuid-root /usr/bin tool gains the owner's effective identity and reaches a root-only
    /// file, while the same command without the setuid file — or without execute permission on
    /// it — stays at the session identity and is denied.
    /// </summary>
    [TestFixture]
    public sealed class SetuidExecutionTests
    {
        private const int Rwsr_xr_x = PermissionMode.SetUidBit | 0b111_101_101;
        private const int Rws______ = PermissionMode.SetUidBit | 0b111_000_000;
        private static readonly Credentials Root = new Credentials(0);

        private static ShellHarness SeededPlayer()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.SeedUsers();
            harness.SeedBin();
            harness.Vfs.CreateFile("/root/secret", new PermissionMode(0b110_000_000), Root);
            var stream = harness.Vfs.OpenForWrite("/root/secret", WriteBehavior.Truncate, new PermissionMode(0b110_000_000), Root);
            var bytes = System.Text.Encoding.UTF8.GetBytes("top-secret");
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
            return harness;
        }

        private static void WriteAsRoot(ShellHarness harness, string path, PermissionMode mode, string content)
        {
            harness.Vfs.CreateFile(path, mode, Root);
            var stream = harness.Vfs.OpenForWrite(path, WriteBehavior.Truncate, mode, Root);
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
        }

        [Test]
        public void A_setuid_root_executable_runs_with_the_owner_identity()
        {
            var harness = SeededPlayer();
            harness.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(Rwsr_xr_x), Root);

            harness.Run("cat /root/secret");

            Assert.That(harness.DrainOutput(), Is.EqualTo("top-secret"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void A_non_setuid_command_runs_with_the_session_identity()
        {
            var harness = SeededPlayer();

            harness.Run("cat /root/secret");

            Assert.That(harness.DrainError(), Does.Contain("Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void A_setuid_file_without_execute_permission_does_not_elevate()
        {
            var harness = SeededPlayer();
            harness.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(Rws______), Root);

            harness.Run("cat /root/secret");

            Assert.That(harness.DrainError(), Does.Contain("Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Group_execute_on_the_setuid_file_permits_elevation()
        {
            var harness = SeededPlayer();
            harness.Session.Credentials = new Credentials(1000, 500);
            harness.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(PermissionMode.SetUidBit | 0b111_101_100), Root);
            harness.Vfs.Chown("/usr/bin/cat", 0, 500, Root);

            harness.Run("cat /root/secret");

            Assert.That(harness.DrainOutput(), Is.EqualTo("top-secret"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void A_group_member_without_group_execute_does_not_elevate()
        {
            var harness = SeededPlayer();
            harness.Session.Credentials = new Credentials(1000, 500);
            // group r-- (no x) while other has r-x: elevation must consult the GROUP class, not other.
            harness.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(PermissionMode.SetUidBit | 0b111_100_101), Root);
            harness.Vfs.Chown("/usr/bin/cat", 0, 500, Root);

            harness.Run("cat /root/secret");

            Assert.That(harness.DrainError(), Does.Contain("Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void A_symlink_at_the_bin_path_does_not_borrow_a_setuid_target()
        {
            var viaSymlink = SeededPlayer();
            viaSymlink.Vfs.CreateDirectory("/opt", new PermissionMode(0b111_101_101), Root);
            viaSymlink.Vfs.CreateFile("/opt/tool", new PermissionMode(Rwsr_xr_x), Root);
            viaSymlink.Vfs.CreateSymlink("/usr/bin/cat", "/opt/tool", Root);

            viaSymlink.Run("cat /root/secret");

            Assert.That(viaSymlink.DrainError(), Does.Contain("Permission denied"));
            Assert.That(viaSymlink.Session.LastExitCode, Is.EqualTo(1));

            var direct = SeededPlayer();
            direct.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(Rwsr_xr_x), Root);

            direct.Run("cat /root/secret");

            Assert.That(direct.DrainOutput(), Is.EqualTo("top-secret"));
            Assert.That(direct.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void A_root_session_is_not_demoted_by_a_setuid_file_owned_by_another_user()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            harness.SeedBin();
            WriteAsRoot(harness, "/root/secret", new PermissionMode(0b110_000_000), "top-secret");
            harness.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(Rwsr_xr_x), Root);
            harness.Vfs.Chown("/usr/bin/cat", 1000, 1000, Root);

            harness.Run("cat /root/secret");

            Assert.That(harness.DrainOutput(), Is.EqualTo("top-secret"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void A_setuid_file_named_like_a_builtin_does_not_elevate_the_builtin()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.SeedUsers();
            harness.SeedBin();
            harness.Vfs.CreateDirectory("/vault", new PermissionMode(0b111_000_000), Root);
            harness.Vfs.CreateFile("/usr/bin/cd", new PermissionMode(PermissionMode.SetUidBit | 0b111_101_101), Root);

            harness.Run("cd /vault");

            Assert.That(harness.DrainError(), Does.Contain("Permission denied"));
            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Owner_match_with_owner_execute_permits_elevation()
        {
            var harness = SeededPlayer();
            harness.Vfs.CreateDirectory("/shared", new PermissionMode(0b111_101_101), Root);
            WriteAsRoot(harness, "/shared/g500", new PermissionMode(0b000_100_000), "group-secret");
            harness.Vfs.Chown("/shared/g500", 0, 500, Root);
            harness.Vfs.CreateFile("/usr/bin/cat", new PermissionMode(PermissionMode.SetUidBit | 0b111_000_000), Root);
            harness.Vfs.Chown("/usr/bin/cat", 1000, 500, Root);

            harness.Run("cat /shared/g500");

            Assert.That(harness.DrainOutput(), Is.EqualTo("group-secret"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }
    }
}
