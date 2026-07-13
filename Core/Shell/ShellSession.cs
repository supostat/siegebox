using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// The live, mutable shell state. Immutable <see cref="ExecutionContext"/> snapshots are
    /// emitted per spawn; builtins mutate this object (or a clone, in subshell positions).
    /// </summary>
    public sealed class ShellSession
    {
        public ShellSession(string workingDirectory, Credentials credentials)
        {
            if (workingDirectory is null)
            {
                throw new ArgumentNullException(nameof(workingDirectory));
            }

            if (string.IsNullOrWhiteSpace(workingDirectory))
            {
                throw new ArgumentException("Working directory must not be empty.", nameof(workingDirectory));
            }

            if (credentials is null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            WorkingDirectory = workingDirectory;
            Credentials = credentials;
            Environment = new Dictionary<string, string>();
        }

        private ShellSession(ShellSession source)
        {
            WorkingDirectory = source.WorkingDirectory;
            Credentials = source.Credentials;
            Environment = new Dictionary<string, string>(source.Environment);
            LastExitCode = source.LastExitCode;
        }

        public string WorkingDirectory { get; set; }

        public Credentials Credentials { get; set; }

        public Dictionary<string, string> Environment { get; }

        public int LastExitCode { get; set; }

        public ExecutionContext CreateContext(FileDescriptorTable fileDescriptors)
            => new ExecutionContext(WorkingDirectory, Credentials, Environment, fileDescriptors);

        public ShellSession Clone() => new ShellSession(this);

        public SessionSnapshot ToSnapshot()
        {
            var groupIds = new List<int>(Credentials.Gids);
            groupIds.Sort();
            return new SessionSnapshot
            {
                Uid = Credentials.Uid,
                Gids = groupIds,
                WorkingDirectory = WorkingDirectory,
                Environment = new Dictionary<string, string>(Environment),
                LastExitCode = LastExitCode
            };
        }

        public void ApplySnapshot(SessionSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (string.IsNullOrWhiteSpace(snapshot.WorkingDirectory))
            {
                throw new ArgumentException("Working directory must not be empty.", nameof(snapshot));
            }

            WorkingDirectory = snapshot.WorkingDirectory;
            Credentials = new Credentials(snapshot.Uid, snapshot.Gids.ToArray());
            Environment.Clear();
            foreach (var entry in snapshot.Environment)
            {
                Environment[entry.Key] = entry.Value;
            }

            LastExitCode = snapshot.LastExitCode;
        }
    }
}
