using System.Collections.Generic;

namespace Siegebox.Shell
{
    /// <summary>
    /// The serializable state of one <see cref="ShellSession"/>: the caller identity, working
    /// directory, exported environment and last exit code. Plain get/set so it round-trips
    /// through a data codec without engine or reflection surprises.
    /// </summary>
    public sealed class SessionSnapshot
    {
        public int Uid { get; set; }

        public List<int> Gids { get; set; } = new List<int>();

        public string WorkingDirectory { get; set; } = string.Empty;

        public Dictionary<string, string> Environment { get; set; } = new Dictionary<string, string>();

        public int LastExitCode { get; set; }
    }
}
