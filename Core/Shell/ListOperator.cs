namespace Siegebox.Shell
{
    /// <summary>How a list item chains on the previous item's exit code.</summary>
    public enum ListOperator
    {
        Always,
        AndIf,
        OrIf
    }
}
