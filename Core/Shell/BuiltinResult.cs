using System;

namespace Siegebox.Shell
{
    public sealed class BuiltinResult
    {
        private BuiltinResult(int exitCode, string output, string error, int waitForPid, string? readLinePrompt, bool secret)
        {
            ExitCode = exitCode;
            Output = output;
            Error = error;
            WaitForPid = waitForPid;
            ReadLinePrompt = readLinePrompt;
            Secret = secret;
        }

        public int ExitCode { get; }

        public string Output { get; }

        public string Error { get; }

        /// <summary>When positive, the executing process sleeps until this pid exits, then re-invokes the builtin.</summary>
        public int WaitForPid { get; }

        /// <summary>
        /// When non-null, the executing process writes this prompt to stdout, reads one line
        /// from stdin, and re-invokes the builtin with that line as <c>inputLine</c>.
        /// </summary>
        public string? ReadLinePrompt { get; }

        /// <summary>When true, the awaited line is a secret and the terminal suppresses its echo.</summary>
        public bool Secret { get; }

        public static BuiltinResult Completed(int exitCode, string output = "", string error = "")
        {
            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (error is null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            return new BuiltinResult(exitCode, output, error, 0, null, false);
        }

        public static BuiltinResult WaitFor(int pid)
        {
            if (pid <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pid), pid, "Pid must be positive.");
            }

            return new BuiltinResult(0, "", "", pid, null, false);
        }

        public static BuiltinResult ReadLine(string prompt, bool secret = false)
        {
            if (prompt is null)
            {
                throw new ArgumentNullException(nameof(prompt));
            }

            return new BuiltinResult(0, "", "", 0, prompt, secret);
        }
    }
}
