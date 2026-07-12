using System;
using MoonSharp.Interpreter;

namespace Siegebox.Scripting
{
    /// <summary>
    /// One sandboxed Lua script per mod with budgeted execution. <see cref="RunChunk"/> and
    /// full calls get the whole slice allowance; hook calls run under the small
    /// <see cref="HookMaxSlices"/> bound so a slow handler cannot stall a frame. Exceeding
    /// a budget throws <see cref="LuaBudgetExceededException"/>; the host stays usable.
    /// </summary>
    public sealed class LuaHost
    {
        public const int DefaultInstructionsPerSlice = 100_000;
        public const int DefaultMaxSlices = 100;
        public const int HookMaxSlices = 2;

        private static readonly DynValue[] NoArguments = Array.Empty<DynValue>();

        private readonly Script script;

        public LuaHost()
        {
            script = LuaSandbox.CreateScript();
        }

        internal Script Script => script;

        public DynValue RunChunk(string code, string chunkName)
        {
            if (code is null)
            {
                throw new ArgumentNullException(nameof(code));
            }

            if (chunkName is null)
            {
                throw new ArgumentNullException(nameof(chunkName));
            }

            if (string.IsNullOrWhiteSpace(chunkName))
            {
                throw new ArgumentException("A chunk name must not be blank.", nameof(chunkName));
            }

            var chunk = script.LoadString(code, null, chunkName);
            return RunBudgeted(chunk, NoArguments, chunkName, DefaultMaxSlices);
        }

        internal DynValue CallToCompletion(DynValue function, DynValue[] arguments, string chunkName)
            => RunBudgeted(function, arguments, chunkName, DefaultMaxSlices);

        internal DynValue CallBounded(DynValue function, DynValue[] arguments, string chunkName)
            => RunBudgeted(function, arguments, chunkName, HookMaxSlices);

        internal LuaCall BeginCall(DynValue function, DynValue[] arguments, string chunkName)
            => new LuaCall(script, function, arguments, chunkName, DefaultInstructionsPerSlice, DefaultMaxSlices);

        private DynValue RunBudgeted(DynValue function, DynValue[] arguments, string chunkName, int maxSlices)
        {
            var call = new LuaCall(script, function, arguments, chunkName, DefaultInstructionsPerSlice, maxSlices);
            while (true)
            {
                var step = call.Step(DynValue.Nil);
                switch (step.Kind)
                {
                    case LuaCall.StepKind.Completed:
                        return step.Value;
                    case LuaCall.StepKind.Yielded:
                        throw new ScriptRuntimeException($"'{chunkName}' yielded outside a command context.");
                }
            }
        }
    }
}
