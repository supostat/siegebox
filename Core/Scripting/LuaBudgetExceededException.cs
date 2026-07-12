using System;

namespace Siegebox.Scripting
{
    /// <summary>Raised when a budgeted Lua call runs past its total instruction allowance.</summary>
    public sealed class LuaBudgetExceededException : Exception
    {
        public LuaBudgetExceededException(string chunkName, long instructionBudget)
            : base($"Lua chunk '{chunkName}' exceeded its budget of {instructionBudget} instructions.")
        {
            ChunkName = chunkName;
            InstructionBudget = instructionBudget;
        }

        public string ChunkName { get; }

        public long InstructionBudget { get; }
    }
}
