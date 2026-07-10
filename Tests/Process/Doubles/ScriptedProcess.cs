using System;

namespace Siegebox.Process.Tests
{
    internal sealed class ScriptedProcess : IProcess
    {
        private readonly Func<ScriptedProcess, ProcessState> script;

        public ScriptedProcess(ExecutionContext context, Func<ScriptedProcess, ProcessState> script)
        {
            Context = context;
            this.script = script;
        }

        public ExecutionContext Context { get; }

        public int ExitCode { get; set; }

        public WakeCondition WakeCondition { get; set; }

        public int StepCount { get; private set; }

        public ProcessState Step()
        {
            StepCount++;
            return script(this);
        }
    }
}
