using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins the ls long format: a mode string, owner name (resolved through the user db),
    /// numeric group and size per entry, and the setuid s-bit made visible on a setuid file.
    /// </summary>
    [TestFixture]
    public sealed class LsLongFormatTests
    {
        private static readonly Credentials Root = new Credentials(0);

        [Test]
        public void Ls_l_renders_mode_owner_name_and_numeric_group()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            harness.Vfs.CreateFile("/pf", new PermissionMode(0b110_100_100), Root);
            harness.Vfs.Chown("/pf", 1000, 1000, Root);

            harness.Run("ls -l /pf");

            Assert.That(harness.DrainOutput(), Is.EqualTo("-rw-r--r--  player  1000  0  /pf\n"));
        }

        [Test]
        public void Ls_l_shows_the_s_bit_on_a_setuid_file()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            harness.SeedBin();

            harness.Run("ls -l /usr/bin");

            var output = harness.DrainOutput();
            Assert.That(output, Does.Contain("-rwsr-xr-x"));
            Assert.That(output, Does.Contain("root"));
            Assert.That(output, Does.Contain("passwd"));
        }

        [Test]
        public void Ls_l_falls_back_to_the_numeric_uid_for_an_unknown_owner()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            harness.Vfs.CreateFile("/orphan", new PermissionMode(0b110_100_100), Root);
            harness.Vfs.Chown("/orphan", 4242, 4242, Root);

            harness.Run("ls -l /orphan");

            Assert.That(harness.DrainOutput(), Is.EqualTo("-rw-r--r--  4242  4242  0  /orphan\n"));
        }

        [Test]
        public void Ls_l_renders_the_type_character_for_directories_symlinks_and_devices()
        {
            var harness = new ShellHarness(uid: 0);
            harness.SeedUsers();
            harness.Vfs.CreateDirectory("/top", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateDirectory("/top/sub", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateSymlink("/top/link", "/top/sub", Root);

            harness.Run("ls -l /top");
            var listing = harness.DrainOutput();
            Assert.That(listing, Does.Contain("drwxr-xr-x"));
            Assert.That(listing, Does.Contain("lrwxrwxrwx"));

            harness.Run("ls -l /dev");
            Assert.That(harness.DrainOutput(), Does.Contain("crw-rw-rw-"));
        }

        [Test]
        public void Ls_l_reports_a_child_stat_failure_without_crashing()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.SeedUsers();
            harness.Vfs.CreateDirectory("/rdir", new PermissionMode(0b111_111_100), Root);
            harness.Vfs.CreateFile("/rdir/child", new PermissionMode(0b110_100_100), Root);

            Assert.That(() => harness.Run("ls -l /rdir"), Throws.Nothing);

            Assert.That(harness.DrainOutput(), Is.Empty);
            Assert.That(harness.DrainError(), Does.Contain("ls: /rdir/child: Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }
    }
}
