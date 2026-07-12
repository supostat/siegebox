using NUnit.Framework;
using Siegebox.App;
using Siegebox.App.Tests;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class OpenCommandTests
    {
        private static readonly AppDescriptor FilesDescriptor =
            new AppDescriptor("files", "files", () => new FakeApp());

        private static ShellHarness HarnessWithOpen(IAppLauncher launcher)
        {
            var harness = new ShellHarness();
            var apps = new AppRegistry();
            apps.Register(FilesDescriptor);
            harness.RegisterCommand(new OpenCommand(apps, launcher));
            return harness;
        }

        [Test]
        public void Open_launches_the_registered_app()
        {
            var launcher = new RecordingAppLauncher();
            var harness = HarnessWithOpen(launcher);

            harness.Run("open files");

            Assert.That(launcher.Launched, Has.Count.EqualTo(1));
            Assert.That(launcher.Launched[0], Is.SameAs(FilesDescriptor));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.DrainError(), Is.EqualTo(""));
        }

        [Test]
        public void Open_without_exactly_one_argument_prints_usage()
        {
            var launcher = new RecordingAppLauncher();
            var harness = HarnessWithOpen(launcher);

            harness.Run("open");
            Assert.That(harness.DrainError(), Does.Contain("open: usage: open app"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));

            harness.Run("open files terminal");
            Assert.That(harness.DrainError(), Does.Contain("open: usage: open app"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));

            Assert.That(launcher.Launched, Is.Empty);
        }

        [Test]
        public void Open_unknown_app_fails_without_launching()
        {
            var launcher = new RecordingAppLauncher();
            var harness = HarnessWithOpen(launcher);

            harness.Run("open ghost");

            Assert.That(harness.DrainError(), Does.Contain("open: ghost: no such app"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(launcher.Launched, Is.Empty);
        }

        [Test]
        public void Open_reports_a_throwing_launch_as_failure()
        {
            var harness = HarnessWithOpen(new ThrowingAppLauncher());

            harness.Run("open files");

            Assert.That(harness.DrainError(), Does.Contain("open: files: launch failed"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }
    }
}
