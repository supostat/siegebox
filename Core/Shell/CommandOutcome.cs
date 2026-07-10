namespace Siegebox.Shell
{
    internal sealed class CommandOutcome
    {
        private CommandOutcome(int exitCode, string output, string error)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }

        public static CommandOutcome Ok(string output = "") => new CommandOutcome(0, output, "");

        public static CommandOutcome Fail(int exitCode, string error) => new CommandOutcome(exitCode, "", error);

        public static CommandOutcome PartialFail(int exitCode, string output, string error)
            => new CommandOutcome(exitCode, output, error);
    }
}
