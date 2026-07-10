using System;
using System.Collections.Generic;
using Siegebox.Vfs;

namespace Siegebox.Process
{
    /// <summary>
    /// Sole owner of the pid table: spawns, steps, kills, and reaps processes with a budgeted
    /// cooperative round-robin <see cref="Tick"/>. Finished processes stay queryable as corpses
    /// until the next <see cref="Tick"/> reaps them.
    /// </summary>
    public sealed class Scheduler
    {
        public const int DefaultTickBudget = 1024;
        public const int DefaultProbeBudget = 4096;
        public const int InterruptExitCode = 130;

        private static readonly byte[] ProbeBuffer = Array.Empty<byte>();

        private readonly Dictionary<int, ProcessEntry> table = new Dictionary<int, ProcessEntry>();
        private readonly List<int> schedulingOrder = new List<int>();
        private readonly ExitCodeLedger exitCodeLedger = new ExitCodeLedger();
        private readonly int tickBudget;
        private readonly int probeBudget;
        private int nextPid = 1;
        private int cursor;
        private int probesRemaining;
        private bool ticking;

        /// <summary>
        /// <paramref name="tickBudget"/> caps process steps per <see cref="Tick"/>.
        /// <paramref name="probeBudget"/> is a separate per-Tick cap on stream wake-condition
        /// probes, so an accumulation of unsatisfiable sleepers cannot freeze a frame: once it is
        /// exhausted the remaining sleepers simply stay asleep and are probed again next Tick.
        /// <see cref="WakeConditionKind.ProcessExit"/> checks are table lookups and consume
        /// no probe budget.
        /// </summary>
        public Scheduler(int tickBudget = DefaultTickBudget, int probeBudget = DefaultProbeBudget)
        {
            if (tickBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickBudget), tickBudget, "Tick budget must be positive.");
            }

            if (probeBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(probeBudget), probeBudget, "Probe budget must be positive.");
            }

            this.tickBudget = tickBudget;
            this.probeBudget = probeBudget;
        }

        public int ProcessCount => table.Count;

        public int Spawn(ICommand command, ExecutionContext context, IReadOnlyList<string> arguments)
        {
            if (command is null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            var process = command.CreateProcess(context, arguments);
            if (process is null)
            {
                throw new InvalidOperationException($"Command '{command.Name}' created a null process.");
            }

            return Spawn(process, command.Name);
        }

        public int Spawn(IProcess process, string name)
        {
            if (process is null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (name.Length == 0)
            {
                throw new ArgumentException("Process name must not be empty.", nameof(name));
            }

            var pid = nextPid;
            nextPid++;
            table.Add(pid, new ProcessEntry(pid, process, name));
            schedulingOrder.Add(pid);
            return pid;
        }

        public bool Contains(int pid) => table.ContainsKey(pid);

        /// <summary>
        /// Non-destructive look at a retained exit status. False while the process is still
        /// alive and after the status has been collected.
        /// </summary>
        public bool TryPeekExitCode(int pid, out int exitCode) => exitCodeLedger.TryPeek(pid, out exitCode);

        /// <summary>
        /// Collects a retained exit status: returns it and ends retention, exactly once per pid.
        /// </summary>
        public bool TryCollectExitCode(int pid, out int exitCode) => exitCodeLedger.TryCollect(pid, out exitCode);

        public IReadOnlyList<ProcessInfo> ListProcesses()
        {
            var processes = new List<ProcessInfo>(schedulingOrder.Count);
            foreach (var pid in schedulingOrder)
            {
                var entry = table[pid];
                processes.Add(new ProcessInfo(pid, entry.Name, entry.State, entry.Process.Context.Credentials.Uid));
            }

            return processes;
        }

        public ProcessState GetState(int pid) => GetEntry(pid).State;

        public int GetExitCode(int pid)
        {
            var entry = GetEntry(pid);
            if (entry.State != ProcessState.Finished)
            {
                throw new InvalidOperationException($"Process {pid} has not finished.");
            }

            return entry.ExitCode;
        }

        /// <summary>
        /// Terminates the process immediately with <see cref="InterruptExitCode"/>, also between
        /// Ticks. Killing an already finished corpse is a no-op: the first termination wins and
        /// the exit code is never overwritten.
        /// </summary>
        public void Kill(int pid)
        {
            var entry = GetEntry(pid);
            if (entry.State == ProcessState.Finished)
            {
                return;
            }

            entry.ExitCode = InterruptExitCode;
            Terminate(entry);
        }

        public void Tick()
        {
            if (ticking)
            {
                throw new InvalidOperationException("A Tick is already in progress.");
            }

            ticking = true;
            try
            {
                probesRemaining = probeBudget;
                var steps = 0;
                while (steps < tickBudget && schedulingOrder.Count > 0)
                {
                    if (!RunPass(ref steps))
                    {
                        break;
                    }
                }
            }
            finally
            {
                ticking = false;
                Reap();
            }
        }

        private bool RunPass(ref int steps)
        {
            var stepped = false;
            var passLength = schedulingOrder.Count;
            for (var visited = 0; visited < passLength; visited++)
            {
                if (steps >= tickBudget)
                {
                    break;
                }

                if (cursor >= schedulingOrder.Count)
                {
                    cursor = 0;
                }

                var entry = table[schedulingOrder[cursor]];
                cursor++;
                if (TryStep(entry))
                {
                    steps++;
                    stepped = true;
                }
            }

            return stepped;
        }

        private bool TryStep(ProcessEntry entry)
        {
            if (entry.State == ProcessState.Sleeping && !TryWake(entry))
            {
                return false;
            }

            if (entry.State != ProcessState.Running)
            {
                return false;
            }

            var reported = entry.Process.Step();
            if (WasTerminatedDuringStep(entry))
            {
                return true;
            }

            switch (reported)
            {
                case ProcessState.Running:
                    return true;
                case ProcessState.Sleeping:
                    Sleep(entry);
                    return true;
                case ProcessState.Finished:
                    entry.ExitCode = entry.Process.ExitCode;
                    Terminate(entry);
                    return true;
                default:
                    throw new InvalidOperationException($"Process reported unknown state {reported}.");
            }
        }

        private bool TryWake(ProcessEntry entry)
        {
            if (!TrySpendProbeBudget(entry.WakeCondition))
            {
                return false;
            }

            if (!IsSatisfied(entry.WakeCondition))
            {
                return false;
            }

            if (WasTerminatedDuringProbe(entry))
            {
                return false;
            }

            entry.State = ProcessState.Running;
            entry.WakeCondition = WakeCondition.None;
            return true;
        }

        private bool TrySpendProbeBudget(WakeCondition condition)
        {
            if (condition.Kind != WakeConditionKind.StreamReadable && condition.Kind != WakeConditionKind.StreamWritable)
            {
                return true;
            }

            if (probesRemaining == 0)
            {
                return false;
            }

            probesRemaining--;
            return true;
        }

        private static bool WasTerminatedDuringStep(ProcessEntry entry) => entry.State == ProcessState.Finished;

        private static bool WasTerminatedDuringProbe(ProcessEntry entry) => entry.State == ProcessState.Finished;

        private static void Sleep(ProcessEntry entry)
        {
            var condition = entry.Process.WakeCondition;
            if (condition.Kind == WakeConditionKind.None)
            {
                throw new InvalidOperationException("A process returned Sleeping without setting a wake condition.");
            }

            entry.State = ProcessState.Sleeping;
            entry.WakeCondition = condition;
        }

        private void Terminate(ProcessEntry entry)
        {
            entry.State = ProcessState.Finished;
            exitCodeLedger.Retain(entry.Pid, entry.ExitCode);
            entry.Process.Context.FileDescriptors.CloseAll();
        }

        private bool IsSatisfied(WakeCondition condition)
        {
            switch (condition.Kind)
            {
                case WakeConditionKind.StreamReadable:
                    return CanWake(condition.Stream!.Read(ProbeBuffer, 0, 0));
                case WakeConditionKind.StreamWritable:
                    return CanWake(condition.Stream!.Write(ProbeBuffer, 0, 0));
                case WakeConditionKind.ProcessExit:
                    return !table.TryGetValue(condition.Pid, out var target) || target.State == ProcessState.Finished;
                default:
                    throw new InvalidOperationException($"Cannot evaluate a wake condition of kind {condition.Kind}.");
            }
        }

        private static bool CanWake(StreamResult probe) => probe.Status != StreamStatus.WouldBlock;

        private void Reap()
        {
            for (var index = schedulingOrder.Count - 1; index >= 0; index--)
            {
                var pid = schedulingOrder[index];
                if (table[pid].State != ProcessState.Finished)
                {
                    continue;
                }

                table.Remove(pid);
                schedulingOrder.RemoveAt(index);
                if (index < cursor)
                {
                    cursor--;
                }
            }
        }

        private ProcessEntry GetEntry(int pid)
        {
            if (!table.TryGetValue(pid, out var entry))
            {
                throw new ArgumentException($"Unknown pid {pid}.", nameof(pid));
            }

            return entry;
        }

        private sealed class ProcessEntry
        {
            public ProcessEntry(int pid, IProcess process, string name)
            {
                Pid = pid;
                Process = process;
                Name = name;
            }

            public int Pid { get; }

            public IProcess Process { get; }

            public string Name { get; }

            public ProcessState State { get; set; }

            public int ExitCode { get; set; }

            public WakeCondition WakeCondition { get; set; }
        }
    }
}
