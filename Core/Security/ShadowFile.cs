using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Security
{
    /// <summary>
    /// Reads and rewrites the raw /etc/shadow text through the VFS resolver under a caller's
    /// credentials — the persistence door the passwd command uses. Because access flows through
    /// the resolver, a caller whose effective credentials are not root is denied by the file's
    /// 0600 mode (a VfsException) before any write, so a failed read leaves the file untouched.
    /// </summary>
    internal static class ShadowFile
    {
        private static readonly PermissionMode CreateMode = new PermissionMode(0b110_000_000);

        public static string Read(VirtualFileSystem vfs, Credentials credentials)
        {
            var stream = vfs.Open(AuthenticationService.ShadowPath, OpenMode.Read, credentials);
            try
            {
                return ByteStreamText.ReadToEnd(stream);
            }
            finally
            {
                stream.CloseRead();
            }
        }

        public static void Write(VirtualFileSystem vfs, Credentials credentials, string content)
        {
            var stream = vfs.OpenForWrite(AuthenticationService.ShadowPath, WriteBehavior.Truncate, CreateMode, credentials);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                stream.CloseWrite();
            }
        }
    }
}
