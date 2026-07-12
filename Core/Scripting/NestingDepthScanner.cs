namespace Siegebox.Scripting
{
    /// <summary>
    /// Disk-boundary depth guard that shields the recursive-descent parsers (MoonSharp Lua
    /// and its JSON converter) from stack-exhausting input. Both scans are linear,
    /// single-pass and fail closed. The manifest (JSON) scan bounds only bracket/brace
    /// nesting outside double-quoted strings — JSON has no comments, single quotes or
    /// keywords. The script scan is delegated to <see cref="LuaNestingScanner"/>, which is
    /// lexer-aware and additionally bounds block-keyword and unary-operator nesting.
    /// Text deeper than <see cref="MaxDepth"/> on any bounded axis is rejected; an
    /// unterminated string is treated as exceeding rather than swallowing the tail.
    /// </summary>
    internal static class NestingDepthScanner
    {
        public const int MaxDepth = 200;

        public static bool ScriptExceedsMaxDepth(string text) => LuaNestingScanner.Exceeds(text);

        public static bool ManifestExceedsMaxDepth(string text)
        {
            var depth = 0;
            var index = 0;
            while (index < text.Length)
            {
                var character = text[index];
                if (character == '"')
                {
                    index = IndexPastDoubleQuoted(text, index);
                    if (index < 0)
                    {
                        return true;
                    }

                    continue;
                }

                if (IsOpener(character) && ++depth > MaxDepth)
                {
                    return true;
                }

                if (IsCloser(character) && depth > 0)
                {
                    depth--;
                }

                index++;
            }

            return false;
        }

        private static int IndexPastDoubleQuoted(string text, int openingQuoteIndex)
        {
            var index = openingQuoteIndex + 1;
            while (index < text.Length)
            {
                if (text[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (text[index] == '"')
                {
                    return index + 1;
                }

                index++;
            }

            return -1;
        }

        private static bool IsOpener(char character)
            => character == '(' || character == '[' || character == '{';

        private static bool IsCloser(char character)
            => character == ')' || character == ']' || character == '}';
    }
}
