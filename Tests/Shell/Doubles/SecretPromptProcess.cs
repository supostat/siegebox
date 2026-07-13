using System;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell.Tests
{
    /// <summary>
    /// Emits the secret-prompt marker followed by a prompt on stdout (or stderr when
    /// <c>onStandardError</c>), WouldBlock-aware like <c>WriterProcess</c>, then optionally
    /// consumes one line from stdin byte-at-a-time like <c>ReaderProcess</c> (a closed stdin
    /// also finishes it). Drives the terminal's echo-suppression path without su or passwd.
    /// </summary>
    internal sealed class SecretPromptProcess : IProcess
    {
        private readonly byte[] payload;
        private readonly bool onStandardError;
        private readonly bool consumesLine;
        private readonly byte[] readBuffer = new byte[1];
        private int position;
        private bool promptWritten;

        public SecretPromptProcess(ExecutionContext context, string prompt, bool onStandardError, bool consumesLine)
        {
            Context = context;
            payload = Encoding.UTF8.GetBytes(SecretPromptMarker.Sequence + prompt);
            this.onStandardError = onStandardError;
            this.consumesLine = consumesLine;
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; private set; }

        public WakeCondition WakeCondition { get; private set; }

        public ProcessState Step()
        {
            if (!promptWritten)
            {
                return WritePrompt();
            }

            return ConsumeLine();
        }

        private ProcessState WritePrompt()
        {
            var target = Context.FileDescriptors.Get(onStandardError ? FileDescriptorTable.Stderr : FileDescriptorTable.Stdout);
            var result = target.Write(payload, position, payload.Length - position);
            switch (result.Status)
            {
                case StreamStatus.Ok:
                    position += result.Count;
                    if (position < payload.Length)
                    {
                        return ProcessState.Running;
                    }

                    promptWritten = true;
                    if (!consumesLine)
                    {
                        ExitCode = 0;
                        return ProcessState.Finished;
                    }

                    return ProcessState.Running;
                case StreamStatus.WouldBlock:
                    WakeCondition = WakeCondition.Writable(target);
                    return ProcessState.Sleeping;
                case StreamStatus.Eof:
                    ExitCode = 1;
                    return ProcessState.Finished;
                default:
                    throw new InvalidOperationException($"Unexpected stream status {result.Status}.");
            }
        }

        private ProcessState ConsumeLine()
        {
            var standardInput = Context.FileDescriptors.Get(FileDescriptorTable.Stdin);
            while (true)
            {
                var result = standardInput.Read(readBuffer, 0, 1);
                switch (result.Status)
                {
                    case StreamStatus.Ok:
                        if (readBuffer[0] == (byte)'\n')
                        {
                            ExitCode = 0;
                            return ProcessState.Finished;
                        }

                        break;
                    case StreamStatus.WouldBlock:
                        WakeCondition = WakeCondition.Readable(standardInput);
                        return ProcessState.Sleeping;
                    case StreamStatus.Eof:
                        ExitCode = 0;
                        return ProcessState.Finished;
                    default:
                        throw new InvalidOperationException($"Unexpected stream status {result.Status}.");
                }
            }
        }
    }
}
