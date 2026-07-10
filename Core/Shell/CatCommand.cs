using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Streaming relay: file arguments are opened lazily one after another (stdin when there
    /// are none), each Step moves at most one bounded chunk, and backpressure on either side
    /// puts the process to sleep. An unreadable source is reported and skipped; a closed
    /// stdout is a broken pipe and ends the process with exit code 1.
    /// </summary>
    public sealed class CatCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public CatCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "cat";

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return new CatProcess(context, vfs, arguments);
        }

        private sealed class CatProcess : IProcess
        {
            private const int ChunkSize = 4096;

            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;
            private readonly byte[] chunk = new byte[ChunkSize];
            private int chunkOffset;
            private int chunkCount;
            private IByteStream? source;
            private bool readingStdin;
            private bool stdinConsumed;
            private int nextArgumentIndex;
            private PendingWrite? pendingError;

            public CatProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
            {
                Context = context;
                this.vfs = vfs;
                this.arguments = arguments;
            }

            public ExecutionContext Context { get; }

            public int ExitCode { get; private set; }

            public WakeCondition WakeCondition { get; private set; }

            public ProcessState Step()
            {
                if (pendingError is not null)
                {
                    if (pendingError.Advance() == DrainStatus.WouldBlock)
                    {
                        WakeCondition = WakeCondition.Writable(pendingError.Target);
                        return ProcessState.Sleeping;
                    }

                    pendingError = null;
                }

                if (chunkCount > 0)
                {
                    return FlushChunk();
                }

                return source is null ? AcquireNextSource() : ReadChunk();
            }

            private ProcessState AcquireNextSource()
            {
                if (arguments.Count == 0)
                {
                    if (stdinConsumed)
                    {
                        return ProcessState.Finished;
                    }

                    source = Context.FileDescriptors.Get(FileDescriptorTable.Stdin);
                    readingStdin = true;
                    return ProcessState.Running;
                }

                if (nextArgumentIndex == arguments.Count)
                {
                    return ProcessState.Finished;
                }

                var path = ShellPath.Absolute(Context.WorkingDirectory, arguments[nextArgumentIndex]);
                nextArgumentIndex++;
                try
                {
                    source = vfs.Open(path, OpenMode.Read, Context.Credentials);
                }
                catch (VfsException failure)
                {
                    ExitCode = 1;
                    pendingError = new PendingWrite(
                        Context.FileDescriptors.Get(FileDescriptorTable.Stderr),
                        Encoding.UTF8.GetBytes($"cat: {failure.Path}: {VfsErrorText.MessageFor(failure.Error)}\n"));
                }

                return ProcessState.Running;
            }

            private ProcessState FlushChunk()
            {
                var standardOutput = Context.FileDescriptors.Get(FileDescriptorTable.Stdout);
                var result = standardOutput.Write(chunk, chunkOffset, chunkCount);
                switch (result.Status)
                {
                    case StreamStatus.Ok:
                        chunkOffset += result.Count;
                        chunkCount -= result.Count;
                        return ProcessState.Running;
                    case StreamStatus.WouldBlock:
                        WakeCondition = WakeCondition.Writable(standardOutput);
                        return ProcessState.Sleeping;
                    default:
                        ExitCode = 1;
                        return ProcessState.Finished;
                }
            }

            private ProcessState ReadChunk()
            {
                var result = source!.Read(chunk, 0, ChunkSize);
                switch (result.Status)
                {
                    case StreamStatus.Ok:
                        chunkOffset = 0;
                        chunkCount = result.Count;
                        return ProcessState.Running;
                    case StreamStatus.WouldBlock:
                        WakeCondition = WakeCondition.Readable(source);
                        return ProcessState.Sleeping;
                    default:
                        stdinConsumed |= readingStdin;
                        readingStdin = false;
                        source = null;
                        return ProcessState.Running;
                }
            }
        }
    }
}
