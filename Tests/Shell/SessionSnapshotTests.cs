using System;
using System.Collections.Generic;
using System.Text.Json;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Pins the shell-session persistence contract: <see cref="ShellSession.ToSnapshot"/>
    /// captures identity, working directory, environment and exit code with gids in a
    /// deterministic order; the snapshot round-trips through a data codec; and
    /// <see cref="ShellSession.ApplySnapshot"/> restores every field and rejects malformed input.
    /// </summary>
    [TestFixture]
    public sealed class SessionSnapshotTests
    {
        private static ShellSession NewSession()
        {
            var session = new ShellSession("/home/player", new Credentials(1000, 40, 10, 25));
            session.Environment["PATH"] = "/usr/bin";
            session.Environment["HOME"] = "/home/player";
            session.LastExitCode = 7;
            return session;
        }

        [Test]
        public void ToSnapshot_captures_state_with_gids_sorted()
        {
            var snapshot = NewSession().ToSnapshot();

            Assert.That(snapshot.Uid, Is.EqualTo(1000));
            Assert.That(snapshot.Gids, Is.EqualTo(new[] { 10, 25, 40 }));
            Assert.That(snapshot.WorkingDirectory, Is.EqualTo("/home/player"));
            Assert.That(snapshot.LastExitCode, Is.EqualTo(7));
            Assert.That(snapshot.Environment["PATH"], Is.EqualTo("/usr/bin"));
        }

        [Test]
        public void Snapshot_round_trips_through_json_and_restores_identity_and_environment()
        {
            var json = JsonSerializer.Serialize(NewSession().ToSnapshot());
            var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(json);

            var restored = new ShellSession("/", new Credentials(0));
            restored.ApplySnapshot(snapshot);

            Assert.That(restored.WorkingDirectory, Is.EqualTo("/home/player"));
            Assert.That(restored.Credentials.Uid, Is.EqualTo(1000));
            Assert.That(restored.Credentials.InGroup(25), Is.True);
            Assert.That(restored.LastExitCode, Is.EqualTo(7));
            Assert.That(restored.Environment["HOME"], Is.EqualTo("/home/player"));
        }

        [Test]
        public void ApplySnapshot_replaces_the_existing_environment_wholesale()
        {
            var restored = new ShellSession("/", new Credentials(0));
            restored.Environment["STALE"] = "value";

            restored.ApplySnapshot(new SessionSnapshot
            {
                Uid = 0,
                Gids = new List<int>(),
                WorkingDirectory = "/",
                Environment = new Dictionary<string, string>(),
                LastExitCode = 0
            });

            Assert.That(restored.Environment, Is.Empty);
        }

        [Test]
        public void ApplySnapshot_rejects_a_null_snapshot()
        {
            var session = new ShellSession("/", new Credentials(0));

            Assert.Throws<ArgumentNullException>(() => session.ApplySnapshot(null));
        }

        [Test]
        public void ApplySnapshot_rejects_a_blank_working_directory()
        {
            var session = new ShellSession("/", new Credentials(0));
            var snapshot = new SessionSnapshot { WorkingDirectory = "   " };

            Assert.Throws<ArgumentException>(() => session.ApplySnapshot(snapshot));
        }
    }
}
