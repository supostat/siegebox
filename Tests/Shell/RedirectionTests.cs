using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class RedirectionTests
    {
        [Test]
        public void Output_redirect_creates_the_file_under_the_session_identity()
        {
            var harness = new ShellHarness();

            harness.Run("echo hi > /f");

            Assert.That(harness.ReadFile("/f"), Is.EqualTo("hi\n"));
            Assert.That(harness.Vfs.Stat("/f", new Credentials(0)).OwnerUid, Is.EqualTo(0));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Append_redirect_adds_to_the_existing_content()
        {
            var harness = new ShellHarness();

            harness.Run("echo one > /f");
            harness.Run("echo two >> /f");

            Assert.That(harness.ReadFile("/f"), Is.EqualTo("one\ntwo\n"));
        }

        [Test]
        public void Input_redirect_feeds_the_file_into_stdin()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "from-file");

            harness.Run("cat < /f");

            Assert.That(harness.DrainOutput(), Is.EqualTo("from-file"));
        }

        [Test]
        public void Output_redirect_truncates_the_stale_tail()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "a-very-long-stale-content");

            harness.Run("echo hi > /f");

            Assert.That(harness.ReadFile("/f"), Is.EqualTo("hi\n"));
        }

        [Test]
        public void Redirect_to_dev_null_discards_the_output()
        {
            var harness = new ShellHarness();

            harness.Run("echo hi > /dev/null");

            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Duplicate_output_redirects_open_both_but_only_the_last_receives_the_payload()
        {
            var harness = new ShellHarness();

            harness.Run("echo hi > /a > /b");

            Assert.That(harness.ReadFile("/b"), Is.EqualTo("hi\n"));
            Assert.That(harness.ReadFile("/a"), Is.EqualTo(""));
        }

        [Test]
        public void Redirect_into_an_unwritable_directory_fails_the_stage_and_skips_and_if()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.Vfs.CreateDirectory("/locked", new PermissionMode(0b111_101_101), new Credentials(0));

            harness.Run("echo hi > /locked/f && echo never");

            Assert.That(harness.DrainError(), Does.Contain("/locked/f").And.Contain("Permission denied"));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Input_redirect_from_a_missing_file_fails_the_stage()
        {
            var harness = new ShellHarness();

            harness.Run("cat < /missing");

            Assert.That(harness.DrainError(), Does.Contain("/missing").And.Contain("No such file or directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Output_redirect_creates_the_file_with_the_non_root_session_identity()
        {
            var harness = new ShellHarness(uid: 1000);
            var root = new Credentials(0);
            harness.Vfs.CreateDirectory("/home", new PermissionMode(0b111_101_101), root);
            harness.Vfs.Chown("/home", 1000, 1000, root);

            harness.Run("echo hi > /home/f");

            var info = harness.Vfs.Stat("/home/f", root);
            Assert.That(info.OwnerUid, Is.EqualTo(1000));
            Assert.That(info.GroupGid, Is.EqualTo(1000));
            Assert.That(info.Mode, Is.EqualTo(new PermissionMode(0b110_100_100)));
            Assert.That(harness.ReadFile("/home/f"), Is.EqualTo("hi\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Redirect_onto_an_unwritable_existing_file_fails_the_stage_and_skips_and_if()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.Vfs.CreateFile("/readonly", new PermissionMode(0b100_100_100), new Credentials(0));

            harness.Run("echo hi > /readonly && echo never");

            Assert.That(harness.DrainError(), Does.Contain("/readonly").And.Contain("Permission denied"));
            Assert.That(harness.DrainOutput(), Is.EqualTo(""));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Input_redirect_without_read_permission_fails_the_stage()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.Vfs.CreateFile("/private", new PermissionMode(0b110_000_000), new Credentials(0));

            harness.Run("cat < /private");

            Assert.That(harness.DrainError(), Does.Contain("/private").And.Contain("Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Quoted_redirect_targets_get_quote_removal()
        {
            var harness = new ShellHarness();

            harness.Run("echo hi > \"/my file\"");
            harness.Run("cat < '/my file'");

            Assert.That(harness.ReadFile("/my file"), Is.EqualTo("hi\n"));
            Assert.That(harness.DrainOutput(), Is.EqualTo("hi\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }
    }
}
