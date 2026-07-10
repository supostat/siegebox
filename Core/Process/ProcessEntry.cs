namespace Siegebox.Process
{
    internal sealed class ProcessEntry
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
