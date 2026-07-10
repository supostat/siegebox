using System;
using System.Collections.Generic;
using System.Runtime.ExceptionServices;
using Siegebox.Vfs;

namespace Siegebox.Process
{
    public sealed class FileDescriptorTable
    {
        public const int Stdin = 0;
        public const int Stdout = 1;
        public const int Stderr = 2;

        private readonly Dictionary<int, FileDescriptorEntry> entries;

        public FileDescriptorTable(IByteStream stdin, IByteStream stdout, IByteStream stderr)
        {
            if (stdin is null)
            {
                throw new ArgumentNullException(nameof(stdin));
            }

            if (stdout is null)
            {
                throw new ArgumentNullException(nameof(stdout));
            }

            if (stderr is null)
            {
                throw new ArgumentNullException(nameof(stderr));
            }

            entries = new Dictionary<int, FileDescriptorEntry>
            {
                [Stdin] = new FileDescriptorEntry(stdin, OpenMode.Read),
                [Stdout] = new FileDescriptorEntry(stdout, OpenMode.Write),
                [Stderr] = new FileDescriptorEntry(stderr, OpenMode.Write)
            };
        }

        public IByteStream Get(int descriptor)
        {
            if (!entries.TryGetValue(descriptor, out var entry))
            {
                throw new ArgumentException($"Unknown file descriptor {descriptor}.", nameof(descriptor));
            }

            return entry.Stream;
        }

        /// <summary>
        /// Closes every descriptor in its open direction(s) only: read-mode entries close
        /// their read side, write-mode entries close their write side. Safe to call repeatedly.
        /// A stream that throws while closing does not stop the others: every descriptor is
        /// still processed and the first failure is rethrown afterwards.
        /// </summary>
        public void CloseAll()
        {
            ExceptionDispatchInfo? firstFailure = null;
            foreach (var entry in entries.Values)
            {
                try
                {
                    Close(entry);
                }
                catch (Exception closeFailure)
                {
                    firstFailure ??= ExceptionDispatchInfo.Capture(closeFailure);
                }
            }

            firstFailure?.Throw();
        }

        private static void Close(FileDescriptorEntry entry)
        {
            if (entry.Mode == OpenMode.Read || entry.Mode == OpenMode.ReadWrite)
            {
                entry.Stream.CloseRead();
            }

            if (entry.Mode == OpenMode.Write || entry.Mode == OpenMode.ReadWrite)
            {
                entry.Stream.CloseWrite();
            }
        }

        private readonly struct FileDescriptorEntry
        {
            public FileDescriptorEntry(IByteStream stream, OpenMode mode)
            {
                Stream = stream;
                Mode = mode;
            }

            public IByteStream Stream { get; }

            public OpenMode Mode { get; }
        }
    }
}
