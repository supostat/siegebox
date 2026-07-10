using System;
using Siegebox.Vfs;

namespace Siegebox.Process
{
    /// <summary>
    /// Per-tick budgeted wake-condition evaluator: stream probes spend the probe budget,
    /// <see cref="WakeConditionKind.ProcessExit"/> checks are free table lookups supplied
    /// by the scheduler.
    /// </summary>
    internal sealed class WakeScanner
    {
        private static readonly byte[] ProbeBuffer = Array.Empty<byte>();

        private readonly int probeBudget;
        private readonly Func<int, bool> processExitSatisfied;
        private int probesRemaining;

        public WakeScanner(int probeBudget, Func<int, bool> processExitSatisfied)
        {
            if (probeBudget <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(probeBudget), probeBudget, "Probe budget must be positive.");
            }

            this.probeBudget = probeBudget;
            this.processExitSatisfied = processExitSatisfied;
        }

        public void BeginTick() => probesRemaining = probeBudget;

        public bool ShouldWake(WakeCondition condition)
        {
            if (!TrySpendProbeBudget(condition))
            {
                return false;
            }

            return IsSatisfied(condition);
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

        private bool IsSatisfied(WakeCondition condition)
        {
            switch (condition.Kind)
            {
                case WakeConditionKind.StreamReadable:
                    return CanWake(condition.Stream!.Read(ProbeBuffer, 0, 0));
                case WakeConditionKind.StreamWritable:
                    return CanWake(condition.Stream!.Write(ProbeBuffer, 0, 0));
                case WakeConditionKind.ProcessExit:
                    return processExitSatisfied(condition.Pid);
                default:
                    throw new InvalidOperationException($"Cannot evaluate a wake condition of kind {condition.Kind}.");
            }
        }

        private static bool CanWake(StreamResult probe) => probe.Status != StreamStatus.WouldBlock;
    }
}
