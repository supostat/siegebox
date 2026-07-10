namespace Siegebox.Process
{
    public enum WakeConditionKind
    {
        None = 0,
        StreamReadable,
        StreamWritable,
        ProcessExit
    }
}
