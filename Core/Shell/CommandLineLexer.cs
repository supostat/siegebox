using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    /// <summary>
    /// Single-pass lexer. Word tokens keep quotes and escapes verbatim so operator characters
    /// inside quoted or escaped spans never split a word; unquoting is the expander's job.
    /// </summary>
    public sealed class CommandLineLexer
    {
        public IReadOnlyList<Token> Tokenize(string line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            var tokens = new List<Token>();
            var index = 0;
            while (index < line.Length)
            {
                if (char.IsWhiteSpace(line[index]))
                {
                    index++;
                    continue;
                }

                tokens.Add(IsOperatorCharacter(line[index]) ? LexOperator(line, ref index) : LexWord(line, ref index));
            }

            return tokens;
        }

        private static Token LexOperator(string line, ref int index)
        {
            var start = index;
            var first = line[index];
            index++;
            if (index < line.Length && line[index] == first && (first == '&' || first == '|' || first == '>'))
            {
                index++;
            }

            var text = line.Substring(start, index - start);
            return new Token(OperatorTypeOf(text, start), text, start);
        }

        private static TokenType OperatorTypeOf(string text, int position) => text switch
        {
            "|" => TokenType.Pipe,
            "&" => TokenType.Background,
            ";" => TokenType.Semicolon,
            ">" => TokenType.RedirectOut,
            "<" => TokenType.RedirectIn,
            ">>" => TokenType.RedirectAppend,
            "&&" => TokenType.AndIf,
            "||" => TokenType.OrIf,
            _ => throw new ShellParseException($"unexpected operator '{text}'", position)
        };

        private static Token LexWord(string line, ref int index)
        {
            var start = index;
            while (index < line.Length && !char.IsWhiteSpace(line[index]) && !IsOperatorCharacter(line[index]))
            {
                switch (line[index])
                {
                    case '\'':
                        SkipSingleQuotedSpan(line, ref index);
                        break;
                    case '"':
                        SkipDoubleQuotedSpan(line, ref index);
                        break;
                    case '\\':
                        index = Math.Min(index + 2, line.Length);
                        break;
                    default:
                        index++;
                        break;
                }
            }

            return new Token(TokenType.Word, line.Substring(start, index - start), start);
        }

        private static void SkipSingleQuotedSpan(string line, ref int index)
        {
            var quotePosition = index;
            index++;
            while (index < line.Length && line[index] != '\'')
            {
                index++;
            }

            if (index == line.Length)
            {
                throw new ShellParseException("unterminated single quote", quotePosition);
            }

            index++;
        }

        private static void SkipDoubleQuotedSpan(string line, ref int index)
        {
            var quotePosition = index;
            index++;
            while (index < line.Length && line[index] != '"')
            {
                index = line[index] == '\\' ? Math.Min(index + 2, line.Length) : index + 1;
            }

            if (index == line.Length)
            {
                throw new ShellParseException("unterminated double quote", quotePosition);
            }

            index++;
        }

        private static bool IsOperatorCharacter(char character)
            => character == '|' || character == '&' || character == ';' || character == '>' || character == '<';
    }
}
