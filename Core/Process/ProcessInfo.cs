namespace Siegebox.Process
{
    public readonly struct ProcessInfo
    {
        public ProcessInfo(int pid, string name, ProcessState state, int ownerUid)
        {
            Pid = pid;
            Name = name;
            State = state;
            OwnerUid = ownerUid;
        }

        public int Pid { get; }

        public string Name { get; }

        public ProcessState State { get; }

        public int OwnerUid { get; }
    }
}
