using System;
using System.Collections.Generic;

namespace Siegebox.Terminal
{
    /// <summary>
    /// Bounded command recall: blanks are ignored, consecutive duplicates collapse, the
    /// oldest entry is evicted beyond the cap. Moving down past the newest entry yields
    /// an empty line exactly once.
    /// </summary>
    public sealed class CommandHistory
    {
        public const int MaxEntries = 100;

        private readonly List<string> entries = new List<string>();
        private int cursor;

        public void Add(string line)
        {
            if (line is null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            if (string.IsNullOrWhiteSpace(line) || (entries.Count > 0 && entries[entries.Count - 1] == line))
            {
                cursor = entries.Count;
                return;
            }

            entries.Add(line);
            if (entries.Count > MaxEntries)
            {
                entries.RemoveAt(0);
            }

            cursor = entries.Count;
        }

        public bool TryMoveUp(out string line)
        {
            if (cursor == 0)
            {
                line = "";
                return false;
            }

            cursor--;
            line = entries[cursor];
            return true;
        }

        public bool TryMoveDown(out string line)
        {
            if (cursor >= entries.Count)
            {
                line = "";
                return false;
            }

            cursor++;
            line = cursor == entries.Count ? "" : entries[cursor];
            return true;
        }
    }
}
