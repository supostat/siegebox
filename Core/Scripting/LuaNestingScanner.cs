using System.Collections.Generic;

namespace Siegebox.Scripting
{
    /// <summary>
    /// Lexer-aware depth guard for Lua scripts. A single linear pass bounds three axes of
    /// parser recursion against <see cref="NestingDepthScanner.MaxDepth"/>: bracket/brace
    /// nesting, block-keyword nesting (function/do/if/repeat open, end/until close — while
    /// and for nest through their own 'do', so counting 'do' covers them and keeps
    /// sequential loops balanced), and runs of consecutive prefix unary operators
    /// (not / - / #). Identifiers are consumed with word boundaries, so 'endpoint',
    /// 'donut' and 'functional' never match 'end', 'do' or 'function'. Comments and
    /// strings are skipped as the lexer discards them; brackets or quotes hidden inside
    /// them can neither be miscounted nor hide following code. Unterminated strings,
    /// block comments and long strings fail closed. Over-rejection at depth 200 is
    /// acceptable and never reaches realistic mod code.
    /// </summary>
    internal static class LuaNestingScanner
    {
        private const int Rejected = -1;

        private static readonly HashSet<string> BlockOpeners =
            new HashSet<string> { "function", "do", "if", "repeat" };

        private static readonly HashSet<string> BlockEnders =
            new HashSet<string> { "end", "until" };

        public static bool Exceeds(string text)
        {
            var state = new ScanState();
            var index = 0;
            while (index < text.Length)
            {
                index = Step(text, index, state);
                if (index == Rejected)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Step(string text, int index, ScanState state)
        {
            var character = text[index];
            if (IsWhitespace(character))
            {
                return index + 1;
            }

            if (character == '-' && CharAt(text, index + 1) == '-')
            {
                return SkipComment(text, index + 2);
            }

            if (character == '-' || character == '#')
            {
                return state.OpenUnary() ? Rejected : index + 1;
            }

            if (character == '[' && TryLongBracketLevel(text, index, out var level))
            {
                return SkipOperandRegion(SkipLongBracket(text, index, level), state);
            }

            if (character == '"' || character == '\'')
            {
                return SkipOperandRegion(IndexPastShortString(text, index), state);
            }

            return StepToken(text, index, character, state);
        }

        private static int StepToken(string text, int index, char character, ScanState state)
            => IsIdentifierStart(character)
                ? ApplyWord(text, index, state)
                : ApplySymbol(character, index, state);

        private static int ApplyWord(string text, int index, ScanState state)
        {
            index = ConsumeWord(text, index, out var word);
            if (word == "not")
            {
                return state.OpenUnary() ? Rejected : index;
            }

            if (BlockOpeners.Contains(word))
            {
                return state.OpenBlock() ? Rejected : index;
            }

            if (BlockEnders.Contains(word))
            {
                state.CloseBlock();
                return index;
            }

            state.EndUnaryRun();
            return index;
        }

        private static int ApplySymbol(char character, int index, ScanState state)
        {
            if (IsOpener(character))
            {
                return state.OpenBracket() ? Rejected : index + 1;
            }

            if (IsCloser(character))
            {
                state.CloseBracket();
                return index + 1;
            }

            state.EndUnaryRun();
            return index + 1;
        }

        private static int SkipOperandRegion(int skippedTo, ScanState state)
        {
            if (skippedTo < 0)
            {
                return Rejected;
            }

            state.EndUnaryRun();
            return skippedTo;
        }

        private static int SkipComment(string text, int index)
        {
            if (index < text.Length && text[index] == '[' && TryLongBracketLevel(text, index, out var level))
            {
                return SkipLongBracket(text, index, level);
            }

            while (index < text.Length && text[index] != '\n')
            {
                index++;
            }

            return index;
        }

        private static bool TryLongBracketLevel(string text, int index, out int level)
        {
            var cursor = index + 1;
            level = 0;
            while (cursor < text.Length && text[cursor] == '=')
            {
                level++;
                cursor++;
            }

            return cursor < text.Length && text[cursor] == '[';
        }

        private static int SkipLongBracket(string text, int index, int level)
        {
            var cursor = index + level + 2;
            while (cursor < text.Length)
            {
                if (text[cursor] == ']' && MatchesLongClose(text, cursor, level))
                {
                    return cursor + level + 2;
                }

                cursor++;
            }

            return Rejected;
        }

        private static bool MatchesLongClose(string text, int index, int level)
        {
            if (index + level + 1 >= text.Length)
            {
                return false;
            }

            for (var offset = 1; offset <= level; offset++)
            {
                if (text[index + offset] != '=')
                {
                    return false;
                }
            }

            return text[index + level + 1] == ']';
        }

        private static int IndexPastShortString(string text, int openingQuoteIndex)
        {
            var quote = text[openingQuoteIndex];
            var index = openingQuoteIndex + 1;
            while (index < text.Length)
            {
                if (text[index] == '\\')
                {
                    index += 2;
                    continue;
                }

                if (text[index] == quote)
                {
                    return index + 1;
                }

                index++;
            }

            return Rejected;
        }

        private static int ConsumeWord(string text, int index, out string word)
        {
            var start = index;
            while (index < text.Length && IsIdentifierPart(text[index]))
            {
                index++;
            }

            word = text.Substring(start, index - start);
            return index;
        }

        private static char CharAt(string text, int index)
            => index < text.Length ? text[index] : '\0';

        private static bool IsWhitespace(char character)
            => character == ' ' || character == '\t' || character == '\n' || character == '\r' || character == '\f' || character == '\v';

        private static bool IsIdentifierStart(char character)
            => (character >= 'a' && character <= 'z') || (character >= 'A' && character <= 'Z') || character == '_';

        private static bool IsIdentifierPart(char character)
            => IsIdentifierStart(character) || (character >= '0' && character <= '9');

        private static bool IsOpener(char character)
            => character == '(' || character == '[' || character == '{';

        private static bool IsCloser(char character)
            => character == ')' || character == ']' || character == '}';

        private sealed class ScanState
        {
            private int brackets;
            private int blocks;
            private int unaryRun;

            public bool OpenUnary() => ++unaryRun > NestingDepthScanner.MaxDepth;

            public void EndUnaryRun() => unaryRun = 0;

            public bool OpenBlock()
            {
                EndUnaryRun();
                return ++blocks > NestingDepthScanner.MaxDepth;
            }

            public void CloseBlock()
            {
                EndUnaryRun();
                if (blocks > 0)
                {
                    blocks--;
                }
            }

            public bool OpenBracket()
            {
                EndUnaryRun();
                return ++brackets > NestingDepthScanner.MaxDepth;
            }

            public void CloseBracket()
            {
                EndUnaryRun();
                if (brackets > 0)
                {
                    brackets--;
                }
            }
        }
    }
}
