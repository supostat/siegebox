using System;
using MoonSharp.Interpreter;

namespace Siegebox.Scripting
{
    /// <summary>
    /// One budgeted steppable call into Lua: every <see cref="Step"/> resumes the underlying
    /// coroutine for at most one instruction slice. A step past the slice allowance throws
    /// <see cref="LuaBudgetExceededException"/>; the budget deliberately never resets, so a
    /// call that yields to the host keeps one total allowance across its whole life.
    /// </summary>
    internal sealed class LuaCall
    {
        internal enum StepKind
        {
            Completed,
            Yielded,
            SliceExhausted
        }

        internal readonly struct StepResult
        {
            public StepResult(StepKind kind, DynValue value)
            {
                Kind = kind;
                Value = value;
            }

            public StepKind Kind { get; }

            public DynValue Value { get; }
        }

        private enum ResumeMode
        {
            Start,
            AfterForcedSuspension,
            AfterYield
        }

        private readonly Coroutine coroutine;
        private readonly DynValue[] startArguments;
        private readonly string chunkName;
        private readonly int instructionsPerSlice;
        private readonly int maxSlices;
        private ResumeMode nextResume = ResumeMode.Start;
        private int usedSlices;

        public LuaCall(Script script, DynValue function, DynValue[] arguments, string chunkName, int instructionsPerSlice, int maxSlices)
        {
            coroutine = script.CreateCoroutine(function).Coroutine;
            coroutine.AutoYieldCounter = instructionsPerSlice;
            startArguments = arguments;
            this.chunkName = chunkName;
            this.instructionsPerSlice = instructionsPerSlice;
            this.maxSlices = maxSlices;
        }

        public StepResult Step(DynValue resumeValue)
        {
            if (usedSlices >= maxSlices)
            {
                throw new LuaBudgetExceededException(chunkName, (long)maxSlices * instructionsPerSlice);
            }

            usedSlices++;
            var result = Resume(resumeValue);
            switch (coroutine.State)
            {
                case CoroutineState.ForceSuspended:
                    nextResume = ResumeMode.AfterForcedSuspension;
                    return new StepResult(StepKind.SliceExhausted, DynValue.Nil);
                case CoroutineState.Suspended:
                    nextResume = ResumeMode.AfterYield;
                    return new StepResult(StepKind.Yielded, result);
                case CoroutineState.Dead:
                    return new StepResult(StepKind.Completed, result);
                default:
                    throw new InvalidOperationException($"Lua chunk '{chunkName}' left its coroutine in state {coroutine.State}.");
            }
        }

        private DynValue Resume(DynValue resumeValue)
        {
            switch (nextResume)
            {
                case ResumeMode.Start:
                    return coroutine.Resume(startArguments);
                case ResumeMode.AfterForcedSuspension:
                    return coroutine.Resume();
                default:
                    return coroutine.Resume(resumeValue);
            }
        }
    }
}
