namespace Siegebox.Shell
{
    public enum TokenType
    {
        Word,
        Pipe,
        RedirectOut,
        RedirectAppend,
        RedirectIn,
        Background,
        Semicolon,
        AndIf,
        OrIf
    }
}
