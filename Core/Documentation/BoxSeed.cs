using System;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Documentation
{
    /// <summary>
    /// Seeds the per-box manifest as data in the VFS: the root-owned <c>/etc/siegebox</c> directory
    /// and the world-readable <c>box.json</c> that carries the scenario target and the doc-browser
    /// hints. The doc browser reads it under the launching player's identity, never as ambient root.
    /// </summary>
    public static class BoxSeed
    {
        public const string ManifestPath = "/etc/siegebox/box.json";

        private const string ManifestJson =
            @"{""target"":""training-box"",""hints"":[""Try `man ls` to read a manual page."",""Use --help on any command for its usage.""]}";

        private static readonly Credentials Root = new Credentials(0);
        private static readonly PermissionMode DirectoryMode = new PermissionMode(0b111_101_101);
        private static readonly PermissionMode ManifestMode = new PermissionMode(0b110_100_100);

        public static void Seed(VirtualFileSystem vfs)
        {
            if (vfs is null)
            {
                throw new ArgumentNullException(nameof(vfs));
            }

            vfs.CreateDirectory("/etc/siegebox", DirectoryMode, Root);
            var stream = vfs.OpenForWrite(ManifestPath, WriteBehavior.Truncate, ManifestMode, Root);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(ManifestJson);
                stream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                stream.CloseWrite();
            }
        }
    }
}
