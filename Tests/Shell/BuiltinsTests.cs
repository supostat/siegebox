using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class BuiltinsTests
    {
        private static readonly Credentials Root = new Credentials(0);

        [Test]
        public void ReadLine_defaults_to_a_non_secret_prompt()
        {
            Assert.That(BuiltinResult.ReadLine("p").Secret, Is.False);
        }

        [Test]
        public void ReadLine_marked_secret_carries_the_flag()
        {
            Assert.That(BuiltinResult.ReadLine("p", secret: true).Secret, Is.True);
        }

        [Test]
        public void Cd_stores_the_canonical_directory()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/a", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateDirectory("/a/b", new PermissionMode(0b111_101_101), Root);

            harness.Run("cd /a//b/../b/");

            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/a/b"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Cd_dot_dot_goes_to_the_parent()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/a", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateDirectory("/a/b", new PermissionMode(0b111_101_101), Root);
            harness.Run("cd /a/b");

            harness.Run("cd ..");

            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/a"));
        }

        [Test]
        public void Export_is_visible_in_the_next_command_environment()
        {
            var harness = new ShellHarness();
            var probe = new ProbeCommand("probe");
            harness.RegisterCommand(probe);

            harness.Run("export GREETING=hello");
            harness.Run("probe");

            Assert.That(probe.Contexts[0].Environment["GREETING"], Is.EqualTo("hello"));
        }

        [Test]
        public void Builtin_inside_a_pipe_runs_on_a_session_clone()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/tmp", new PermissionMode(0b111_101_101), Root);

            harness.Run("cd /tmp | cat");

            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/"));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(0));
        }

        [Test]
        public void Background_builtin_runs_on_a_session_clone()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/tmp", new PermissionMode(0b111_101_101), Root);

            harness.Run("cd /tmp &");

            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/"));
        }

        [Test]
        public void Cd_without_arguments_prefers_home_and_falls_back_to_root()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/home", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateDirectory("/elsewhere", new PermissionMode(0b111_101_101), Root);
            harness.Session.Environment["HOME"] = "/home";

            harness.Run("cd /elsewhere");
            harness.Run("cd");
            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/home"));

            harness.Session.Environment.Remove("HOME");
            harness.Run("cd /elsewhere");
            harness.Run("cd");
            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/"));
        }

        [Test]
        public void Cd_to_a_file_reports_not_a_directory()
        {
            var harness = new ShellHarness();
            harness.WriteFile("/f", "content");

            harness.Run("cd /f");

            Assert.That(harness.DrainError(), Does.Contain("cd: /f: Not a directory"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/"));
        }

        [Test]
        public void Cd_into_a_directory_without_execute_bit_reports_permission_denied()
        {
            var harness = new ShellHarness(uid: 1000);
            harness.Vfs.CreateDirectory("/sealed", new PermissionMode(0b110_100_100), Root);

            harness.Run("cd /sealed");

            Assert.That(harness.DrainError(), Does.Contain("cd: /sealed: Permission denied"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Cd_with_too_many_arguments_fails()
        {
            var harness = new ShellHarness();

            harness.Run("cd /a /b");

            Assert.That(harness.DrainError(), Does.Contain("cd: too many arguments"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
        }

        [Test]
        public void Export_without_an_assignment_fails()
        {
            var harness = new ShellHarness();

            harness.Run("export bogus");
            Assert.That(harness.DrainError(), Does.Contain("export: bogus"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));

            harness.Run("export =x");
            Assert.That(harness.DrainError(), Does.Contain("export: =x"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(1));
            Assert.That(harness.Session.Environment.ContainsKey(""), Is.False);
        }

        [Test]
        public void Builtin_output_inside_a_pipe_flows_through_the_pipe()
        {
            var harness = new ShellHarness();
            harness.RegisterCommand(new SpinCommand());
            harness.Run("spin &", maxTicks: 8);
            var spinPid = ShellHarness.AnnouncedPid(harness.DrainError());

            harness.Run("jobs | cat");

            Assert.That(harness.DrainOutput(), Is.EqualTo($"[1] {spinPid} Running spin\n"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
            Assert.That(harness.Scheduler.ProcessCount, Is.EqualTo(1));
        }

        [Test]
        public void Cd_through_a_symlink_lands_on_the_canonical_target()
        {
            var harness = new ShellHarness();
            harness.Vfs.CreateDirectory("/a", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateDirectory("/a/b", new PermissionMode(0b111_101_101), Root);
            harness.Vfs.CreateSymlink("/shortcut", "/a/b", Root);

            harness.Run("cd /shortcut");

            Assert.That(harness.Session.WorkingDirectory, Is.EqualTo("/a/b"));
            Assert.That(harness.Session.LastExitCode, Is.EqualTo(0));
        }

        [Test]
        public void Export_overwrites_and_splits_at_the_first_equals()
        {
            var harness = new ShellHarness();

            harness.Run("export A=1");
            harness.Run("export A=2");
            harness.Run("export B=b=c");

            Assert.That(harness.Session.Environment["A"], Is.EqualTo("2"));
            Assert.That(harness.Session.Environment["B"], Is.EqualTo("b=c"));
        }
    }
}
