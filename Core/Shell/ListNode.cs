using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class ListNode : IShellAstNode
    {
        public ListNode(IReadOnlyList<ListItem> items)
        {
            if (items is null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException("A list needs at least one item.", nameof(items));
            }

            Items = items;
        }

        public IReadOnlyList<ListItem> Items { get; }
    }
}
