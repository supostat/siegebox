using System;
using System.Collections.Generic;
using System.Text;

namespace Siegebox.Shell
{
    /// <summary>
    /// Turns raw lexer words into argv values. Variable and glob expansion are identity
    /// stages for now; quote removal strips delimiters and unescapes backslash sequences.
    /// </summary>
    public sealed class ArgumentExpander
    {
        public IReadOnlyList<string> Expand(IReadOnlyList<string> rawWords)
        {
            if (rawWords is null)
            {
                throw new ArgumentNullException(nameof(rawWords));
            }

            var expanded = new List<string>(rawWords.Count);
            foreach (var rawWord in rawWords)
            {
                expanded.Add(ExpandWord(rawWord));
            }

            return expanded;
        }

        public string ExpandWord(string rawWord)
        {
            if (rawWord is null)
            {
                throw new ArgumentNullException(nameof(rawWord));
            }

            return RemoveQuotes(ExpandGlobs(ExpandVariables(rawWord)));
        }

        private static string ExpandVariables(string word) => word;

        private static string ExpandGlobs(string word) => word;

        private static string RemoveQuotes(string word)
        {
            var result = new StringBuilder(word.Length);
            var index = 0;
            while (index < word.Length)
            {
                switch (word[index])
                {
                    case '\'':
                        index = AppendSingleQuotedSpan(word, index, result);
                        break;
                    case '"':
                        index = AppendDoubleQuotedSpan(word, index, result);
                        break;
                    case '\\':
                        index = AppendEscapedCharacter(word, index, result);
                        break;
                    default:
                        result.Append(word[index]);
                        index++;
                        break;
                }
            }

            return result.ToString();
        }

        private static int AppendSingleQuotedSpan(string word, int index, StringBuilder result)
        {
            index++;
            while (index < word.Length && word[index] != '\'')
            {
                result.Append(word[index]);
                index++;
            }

            return index + 1;
        }

        private static int AppendDoubleQuotedSpan(string word, int index, StringBuilder result)
        {
            index++;
            while (index < word.Length && word[index] != '"')
            {
                index = word[index] == '\\'
                    ? AppendEscapedCharacter(word, index, result)
                    : AppendCharacter(word, index, result);
            }

            return index + 1;
        }

        private static int AppendEscapedCharacter(string word, int index, StringBuilder result)
        {
            if (index + 1 == word.Length)
            {
                result.Append('\\');
                return index + 1;
            }

            result.Append(word[index + 1]);
            return index + 2;
        }

        private static int AppendCharacter(string word, int index, StringBuilder result)
        {
            result.Append(word[index]);
            return index + 1;
        }
    }
}
