using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins the chmod command: octal and symbolic setuid forms delegate to the VFS door (so a
    /// non-root u+s is refused with EPERM), and every malformed spec is RETURNED as a failure
    /// line with exit 1 — never thrown, which the buffered-process false-success trap punishes.
    /// </summary>
    [TestFixture]
    public sealed class ChmodCommandTests
    {
        private static readonly Credentials Root = new Credentials(0);

        [Test]
        public void Octal_4755_sets_the_setuid_bit()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateFile("/f", new PermissionMode(0b110_100_100), Root);

            harness.Run("chmod 4755 /f");

            var mode = harness.Vfs.Stat("/f", Root).Mode;
            Assert.That(mode.SetUid, Is.True);
            Assert.That(mode.ToString(), Is.EqualTo("rwsr-xr-x"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Symbolic_u_plus_s_and_u_minus_s_toggle_the_setuid_bit()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateFile("/f", new PermissionMode(0b111_101_101), Root);

            harness.Run("chmod u+s /f");
            Assert.That(harness.Vfs.Stat("/f", Root).Mode.SetUid, Is.True);

            harness.Run("chmod u-s /f");
            Assert.That(harness.Vfs.Stat("/f", Root).Mode.SetUid, Is.False);
        }

        [Test]
        public void A_non_root_owner_cannot_add_setuid_and_the_bit_stays_off()
        {
            var harness = new ShellHarness("/home/player", uid: 1000);
            harness.SeedUsers();
            harness.Run("touch f");

            harness.Run("chmod u+s f");

            Assert.That(harness.DrainError(), Does.Contain("Operation not permitted"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.Vfs.Stat("/home/player/f", Root).Mode.SetUid, Is.False);
        }

        [Test]
        public void A_non_octal_spec_is_reported_as_an_invalid_mode_without_crashing()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateFile("/f", new PermissionMode(0b110_100_100), Root);

            Assert.That(() => harness.Run("chmod 8xy /f"), Throws.Nothing);
            Assert.That(harness.DrainError(), Does.Contain("chmod: invalid mode: '8xy'"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));

            Assert.That(() => harness.Run("chmod zzz /f"), Throws.Nothing);
            Assert.That(harness.DrainError(), Does.Contain("chmod: invalid mode: 'zzz'"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));

            Assert.That(() => harness.Run("chmod 07555 /f"), Throws.Nothing);
            Assert.That(harness.DrainError(), Does.Contain("chmod: invalid mode: '07555'"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void An_unsupported_high_octal_digit_is_an_invalid_mode()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateFile("/f", new PermissionMode(0b110_100_100), Root);

            harness.Run("chmod 2755 /f");

            Assert.That(harness.DrainError(), Does.Contain("chmod: invalid mode: '2755'"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void A_missing_path_operand_is_reported()
        {
            var harness = new ShellHarness();

            harness.Run("chmod 755");

            Assert.That(harness.DrainError(), Does.Contain("chmod: missing operand"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Octal_mode_is_applied_to_every_operand()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateFile("/a", new PermissionMode(0b110_100_100), Root);
            harness.Vfs.CreateFile("/b", new PermissionMode(0b110_100_100), Root);

            harness.Run("chmod 4755 /a /b");

            Assert.That(harness.Vfs.Stat("/a", Root).Mode.SetUid, Is.True);
            Assert.That(harness.Vfs.Stat("/b", Root).Mode.SetUid, Is.True);
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void A_missing_target_is_reported_without_crashing()
        {
            var harness = new ShellHarness();

            Assert.That(() => harness.Run("chmod 755 /nope"), Throws.Nothing);

            Assert.That(harness.DrainError(), Does.Contain("chmod: /nope: No such file or directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Multi_operand_applies_the_valid_paths_and_reports_the_missing_one()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateFile("/a", new PermissionMode(0b110_100_100), Root);
            harness.Vfs.CreateFile("/b", new PermissionMode(0b110_100_100), Root);

            harness.Run("chmod 4755 /a /nope /b");

            Assert.That(harness.Vfs.Stat("/a", Root).Mode.SetUid, Is.True);
            Assert.That(harness.Vfs.Stat("/b", Root).Mode.SetUid, Is.True);
            Assert.That(harness.DrainError(), Does.Contain("chmod: /nope: No such file or directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }
    }
}
