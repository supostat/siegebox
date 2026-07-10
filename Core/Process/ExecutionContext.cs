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

            WorkingDirectory = workingDirectory;
            Credentials = credentials;
            Environment = new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(environment));
            FileDescriptors = fileDescriptors;
        }

        public string WorkingDirectory { get; }

        public Credentials Credentials { get; }

        public IReadOnlyDictionary<string, string> Environment { get; }

        public FileDescriptorTable FileDescriptors { get; }
    }
}
