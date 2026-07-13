using System;
using Siegebox.Vfs;

namespace Siegebox.Security
{
    /// <summary>
    /// Seeds the setuid elevation tools as data in the VFS (the "first mod" installs it next to
    /// <see cref="UserSeed"/>): the world-executable /usr/bin directory (root:root 0755) and the
    /// setuid-root /usr/bin/passwd (root:root, rwsr-xr-x = 04755). The setuid bit on the file is
    /// the sole, visible source of elevation — no ambient capability grants it.
    /// </summary>
    public static class BinSeed
    {
        private const int DirectoryMode = 0b111_101_101;
        private const int SetuidExecutableMode = PermissionMode.SetUidBit | 0b111_101_101;

        private static readonly Credentials Root = new Credentials(0);

        public static void Seed(VirtualFileSystem vfs)
        {
            if (vfs is null)
            {
                throw new ArgumentNullException(nameof(vfs));
            }

            vfs.CreateDirectory("/usr", new PermissionMode(DirectoryMode), Root);
            vfs.CreateDirectory("/usr/bin", new PermissionMode(DirectoryMode), Root);
            vfs.CreateFile("/usr/bin/passwd", new PermissionMode(SetuidExecutableMode), Root);
        }
    }
}
