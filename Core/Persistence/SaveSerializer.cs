using System;
using System.Collections.Generic;
using Siegebox.Events;
using Siegebox.Vfs;

namespace Siegebox.Persistence
{
    /// <summary>
    /// The boundary between a <see cref="SaveGame"/> object graph and validated live kernel
    /// state — not bytes. <see cref="Capture"/> stamps the current version over a freshly
    /// exported tree; <see cref="Load"/> checks the version FIRST, then rejects a missing or
    /// non-directory root, then imports the tree (which enforces its own structural invariants).
    /// The version gate runs before any tree is trusted, so a wrong-version save always fails as
    /// a format error rather than a VFS error.
    /// </summary>
    public static class SaveSerializer
    {
        public static SaveGame Capture(VfsNodeSnapshot root, IReadOnlyList<WindowSnapshot> windows)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            if (windows is null)
            {
                throw new ArgumentNullException(nameof(windows));
            }

            return new SaveGame
            {
                Version = SaveVersion.Current,
                Root = root,
                Windows = new List<WindowSnapshot>(windows)
            };
        }

        public static LoadedSave Load(SaveGame save, EventBus? events = null)
        {
            if (save is null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            SaveVersion.EnsureSupported(save.Version);

            if (save.Root is null)
            {
                throw new SaveFormatException("The save has no root node.");
            }

            if (save.Root.Type != NodeType.Directory)
            {
                throw new SaveFormatException($"The save root must be a directory but was {save.Root.Type}.");
            }

            var vfs = VirtualFileSystem.Import(save.Root, events);
            return new LoadedSave(vfs, save.Windows ?? new List<WindowSnapshot>());
        }
    }
}
