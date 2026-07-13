using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Siegebox.Vfs;

namespace Siegebox.Process
{
    /// <summary>
    /// Immutable per-process snapshot handed to a process at spawn time. The environment is
    /// defensively copied at construction; the descriptor table is shared by reference.
    /// </summary>
    public sealed class ExecutionContext
    {
        public ExecutionContext(
            string workingDirectory,
            Credentials credentials,
            IReadOnlyDictionary<string, string> environment,
            FileDescriptorTable fileDescriptors)
            : this(workingDirectory, credentials, environment, fileDescriptors, credentials)
        {
        }

        public ExecutionContext(
            string workingDirectory,
            Credentials credentials,
            IReadOnlyDictionary<string, string> environment,
            FileDescriptorTable fileDescriptors,
            Credentials realCredentials)
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

            if (environment is null)
            {
                throw new ArgumentNullException(nameof(environment));
            }

            if (fileDescriptors is null)
            {
                throw new ArgumentNullException(nameof(fileDescriptors));
            }

            if (realCredentials is null)
            {
                throw new ArgumentNullException(nameof(realCredentials));
            }

            WorkingDirectory = workingDirectory;
            Credentials = credentials;
            Environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(environment));
            FileDescriptors = fileDescriptors;
            RealCredentials = realCredentials;
        }

        public string WorkingDirectory { get; }

        /// <summary>
        /// The EFFECTIVE identity used for all VFS and permission checks. Under a setuid spawn this
        /// is the executable file's owner (elevated); otherwise it equals <see cref="RealCredentials"/>.
        /// </summary>
        public Credentials Credentials { get; }

        /// <summary>
        /// The REAL identity of the launching session, unchanged by setuid elevation. Used for
        /// authorization decisions (e.g. passwd's policy), never for VFS access.
        /// </summary>
        public Credentials RealCredentials { get; }

        public IReadOnlyDictionary<string, string> Environment { get; }

        public FileDescriptorTable FileDescriptors { get; }
    }
}
