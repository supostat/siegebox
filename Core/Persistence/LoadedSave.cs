using System;
using System.Collections.Generic;
using Siegebox.Vfs;

namespace Siegebox.Persistence
{
    /// <summary>
    /// The validated result of loading a save: a live virtual file system rebuilt from the
    /// imported tree and the window layout to rehydrate on top of it.
    /// </summary>
    public sealed class LoadedSave
    {
        public LoadedSave(VirtualFileSystem vfs, IReadOnlyList<WindowSnapshot> windows)
        {
            Vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            Windows = windows ?? throw new ArgumentNullException(nameof(windows));
        }

        public VirtualFileSystem Vfs { get; }

        public IReadOnlyList<WindowSnapshot> Windows { get; }
    }
}
