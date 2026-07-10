using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Vfs;
using ShellInterpreter = Siegebox.Shell.Shell;

namespace Siegebox.Terminal
{
    /// <summary>
    /// The reader session behind one terminal window: owns the three terminal PipeStreams,
    /// the Shell, the scrollback and the pending-stdin queue. A busy submit routes to stdin;
    /// an idle submit echoes the prompt and executes. Close hangs up the executor and every
    /// job pid (exit 129), collects their statuses, and closes the underlying pipe ends so
    /// foreground members cascade out via Eof / broken pipe.
    /// </summary>
    public sealed class TerminalSession
    {
        public const int MaxPendingInputBytes = 16384;

        private readonly Scheduler scheduler;
        private readonly ShellSession session;
        private readonly JobTable jobs;
        private readonly ShellInterpreter shell;
        private readonly PipeStream terminalInput = new PipeStream();
        private readonly PipeStream terminalOutput = new PipeStream();
        private readonly PipeStream terminalError = new PipeStream();
        private readonly StreamTextDrain outputDrain;
        private readonly StreamTextDrain errorDrain;
        private readonly ScrollbackBuffer scrollback = new ScrollbackBuffer();
        private readonly JobStatusCollector collector;
        private readonly Queue<byte[]> pendingInput = new Queue<byte[]>();
        private int pendingHeadOffset;
        private int pendingByteCount;
        private int activePid;
        private bool closed;

        public TerminalSession(
            Scheduler scheduler,
            VirtualFileSystem vfs,
            CommandRegistry commands,
            BuiltinRegistry builtins,
            ShellSession session,
            JobTable jobs)
        {
            this.scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
            shell = new ShellInterpreter(scheduler, vfs, commands, builtins, session, jobs, terminalInput, terminalOutput, terminalError);
            outputDrain = new StreamTextDrain(terminalOutput);
            errorDrain = new StreamTextDrain(terminalError);
            collector = new JobStatusCollector(scheduler, jobs);
        }

        public bool IsBusy => activePid > 0 && scheduler.Contains(activePid);

        public bool IsClosed => closed;

        public string ScrollbackText => scrollback.Text;

        public int ScrollbackVersion => scrollback.Version;

        public string PromptText => session.WorkingDirectory + (session.Credentials.IsRoot ? " # " : " $ ");

        public bool SubmitLine(string line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (closed)
            {
                return false;
            }

            if (IsBusy)
            {
                return QueueInput(line);
            }

            scrollback.Append(PromptText + line + "\n");
            activePid = shell.Execute(line);
            return true;
        }

        public void Pump()
        {
            if (closed)
            {
                return;
            }

            FlushPendingInput();
            AppendDrained(outputDrain);
            AppendDrained(errorDrain);
            if (!IsBusy)
            {
                NotifyFinishedJobs();
            }
        }

        public void Close()
        {
            if (closed)
            {
                return;
            }

            closed = true;
            if (activePid > 0)
            {
                HangupIfAlive(activePid);
                scheduler.TryCollectExitCode(activePid, out _);
            }

            foreach (var job in new List<Job>(jobs.Jobs))
            {
                foreach (var pid in job.Pids)
                {
                    HangupIfAlive(pid);
                }

                collector.CollectAndRemove(job);
            }

            terminalInput.CloseWrite();
            terminalOutput.CloseRead();
            terminalError.CloseRead();
        }

        private void HangupIfAlive(int pid)
        {
            if (scheduler.Contains(pid))
            {
                scheduler.Hangup(pid);
            }
        }

        private bool QueueInput(string line)
        {
            var bytes = Encoding.UTF8.GetBytes(line + "\n");
            if (pendingByteCount + bytes.Length > MaxPendingInputBytes)
            {
                return false;
            }

            scrollback.Append(line + "\n");
            pendingInput.Enqueue(bytes);
            pendingByteCount += bytes.Length;
            FlushPendingInput();
            return true;
        }

        private void FlushPendingInput()
        {
            while (pendingInput.Count > 0)
            {
                var chunk = pendingInput.Peek();
                var result = terminalInput.Write(chunk, pendingHeadOffset, chunk.Length - pendingHeadOffset);
                if (result.Status != StreamStatus.Ok)
                {
                    return;
                }

                pendingHeadOffset += result.Count;
                pendingByteCount -= result.Count;
                if (pendingHeadOffset == chunk.Length)
                {
                    pendingInput.Dequeue();
                    pendingHeadOffset = 0;
                }
            }
        }

        private void AppendDrained(StreamTextDrain drain)
        {
            var text = drain.Drain();
            if (text.Length > 0)
            {
                scrollback.Append(text);
            }
        }

        private void NotifyFinishedJobs()
        {
            foreach (var job in new List<Job>(jobs.Jobs))
            {
                if (!collector.IsJobFinished(job))
                {
                    continue;
                }

                scrollback.Append($"[{job.Number}] {job.LastPid} Done {job.Description}\n");
                collector.CollectAndRemove(job);
            }
        }
    }
}
