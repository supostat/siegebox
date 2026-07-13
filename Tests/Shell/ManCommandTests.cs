using System.Text;
using NUnit.Framework;
using Siegebox.Documentation;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class ManCommandTests
    {
        private static readonly Credentials Root = new Credentials(0);

        [Test]
        public void Man_prints_the_seeded_page_body()
        {
            var harness = new ShellHarness();
            SeedManPages(harness);

            harness.Run("man cat");

            Assert.That(harness.DrainOutput(), Does.Contain("concatenate files and print on the standard output"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Man_reports_a_missing_operand()
        {
            var harness = new ShellHarness();
            SeedManPages(harness);

            harness.Run("man");

            Assert.That(harness.DrainError(), Does.Contain("man: missing operand"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Man_reports_an_unknown_page_as_not_found()
        {
            var harness = new ShellHarness();
            SeedManPages(harness);

            harness.Run("man nope");

            Assert.That(harness.DrainError(), Does.Contain("man: /usr/share/man/nope: No such file or directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Man_rejects_a_name_with_a_path_separator_as_not_found()
        {
            var harness = new ShellHarness();
            SeedManPages(harness);

            harness.Run("man foo/bar");

            Assert.That(harness.DrainError(), Does.Contain("man: foo/bar: No such file or directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Man_on_an_unreadable_page_reports_permission_denied()
        {
            var harness = new ShellHarness(uid: 1000);
            SeedManPages(harness);
            WriteRootOnlyPage(harness.Vfs, "secret", "classified\n");

            harness.Run("man secret");

            Assert.That(harness.DrainError(), Does.Contain("man: /usr/share/man/secret: Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Man_streams_through_a_pipe()
        {
            var harness = new ShellHarness();
            SeedManPages(harness);

            harness.Run("man cat | cat");

            Assert.That(harness.DrainOutput(), Does.Contain("concatenate files"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Man_redirects_into_a_file()
        {
            var harness = new ShellHarness();
            SeedManPages(harness);

            harness.Run("man cat > /out");

            Assert.That(harness.ReadFile("/out"), Does.Contain("concatenate files"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        private static void SeedManPages(ShellHarness harness)
        {
            harness.SeedBin();
            ManualSeed.SeedPages(harness.Vfs);
        }

        private static void WriteRootOnlyPage(VirtualFileSystem vfs, string name, string body)
        {
            var stream = vfs.OpenForWrite(
                "/usr/share/man/" + name, WriteBehavior.Truncate, new PermissionMode(0b110_000_000), Root);
            var bytes = Encoding.UTF8.GetBytes(body);
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
        }
    }
}
