namespace Siegebox.Shell
{
    /// <summary>A lexed span with quotes and escapes preserved verbatim; expansion strips them later.</summary>
    public readonly struct Token
    {
        public Token(TokenType type, string text, int position)
        {
            Type = type;
            Text = text;
            Position = position;
        }

        public TokenType Type { get; }

        public string Text { get; }

        public int Position { get; }
    }
}
