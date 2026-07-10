namespace Siegebox.Process
{
    /// <summary>
    /// A cooperatively scheduled process instance: each <see cref="Step"/> advances one bounded
    /// slice of work and reports the resulting state.
    /// </summary>
    public interface IProcess
    {
        ExecutionContext Context { get; }

        /// <summary>Meaningful only once the process has reported <see cref="ProcessState.Finished"/>.</summary>
        int ExitCode { get; }

        /// <summary>
        /// MUST be set before <see cref="Step"/> returns <see cref="ProcessState.Sleeping"/>.
        /// The scheduler reads and stores the value once, immediately after <see cref="Step"/>
        /// returns; later mutations are ignored.
        /// </summary>
        WakeCondition WakeCondition { get; }

        ProcessState Step();
    }
}
