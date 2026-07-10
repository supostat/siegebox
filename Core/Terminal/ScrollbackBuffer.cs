using System;
using Siegebox.Shell;

namespace Siegebox.Terminal
{
    /// <summary>
    /// Bounded terminal text: the clear escape sequence (even split across appends) drops
    /// everything through it; overflow head-trims to a line boundary, or hard-cuts when a
    /// single line exceeds the cap. <see cref="Version"/> changes whenever the text does.
    /// </summary>
    public sealed class ScrollbackBuffer
    {
        public const int MaxCharacters = 65536;

        private string buffer = "";

        public string Text => buffer;

        public int Version { get; private set; }

        public void Append(string chunk)
        {
            if (chunk is null)
            {
                throw new ArgumentNullException(nameof(chunk));
            }

            if (chunk.Length == 0)
            {
                return;
            }

            var combined = buffer + chunk;
            var clearIndex = combined.LastIndexOf(ClearCommand.ClearSequence, StringComparison.Ordinal);
            if (clearIndex >= 0)
            {
                combined = combined.Substring(clearIndex + ClearCommand.ClearSequence.Length);
            }

            if (combined.Length > MaxCharacters)
            {
                combined = TrimHeadToLineBoundary(combined);
            }

            buffer = combined;
            Version++;
        }

        public void Clear()
        {
            buffer = "";
            Version++;
        }

        private static string TrimHeadToLineBoundary(string text)
        {
            var minimumCut = text.Length - MaxCharacters;
            var newlineIndex = text.IndexOf('\n', minimumCut - 1 < 0 ? 0 : minimumCut - 1);
            if (newlineIndex < 0)
            {
                return text.Substring(minimumCut);
            }

            return text.Substring(newlineIndex + 1);
        }
    }
}
