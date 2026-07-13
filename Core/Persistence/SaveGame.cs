using System.Collections.Generic;
using Siegebox.Vfs;

namespace Siegebox.Persistence
{
    /// <summary>
    /// The serializable root of a save: its schema version, the exported VFS tree and the
    /// per-window layout. Plain get/set so it round-trips through a data codec as-is; the loader
    /// validates it before any of it is trusted.
    /// </summary>
    public sealed class SaveGame
    {
        public int Version { get; set; }

        public VfsNodeSnapshot? Root { get; set; }

        public List<WindowSnapshot> Windows { get; set; } = new List<WindowSnapshot>();
    }
}
