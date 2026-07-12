namespace Siegebox.App
{
    /// <summary>
    /// Host-agnostic text app surface: a title and a plain-text body. The host renders
    /// Text verbatim with rich text disabled and polls <see cref="Revision"/> from its
    /// per-frame pump — the value increments every time Text changes.
    /// </summary>
    public interface ITextContentApp
    {
        string Title { get; }

        string Text { get; }

        int Revision { get; }
    }
}
