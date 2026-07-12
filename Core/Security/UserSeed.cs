using System;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Security
{
    /// <summary>
    /// Seeds the base user db as data in the VFS (the "first mod" installs it): root (uid 0)
    /// and an unprivileged player (uid 1000), their home directories, world-readable
    /// /etc/passwd and root-only /etc/shadow (0600). Passwords are game defaults — the point
    /// is that shadow holds hashes, not plaintext, so it is an honest gameplay artifact.
    /// </summary>
    public static class UserSeed
    {
        public const int RootUid = 0;
        public const int PlayerUid = 1000;
        public const int PlayerGid = 1000;
        public const string PlayerName = "player";
        public const string PlayerHome = "/home/player";

        private const string RootPassword = "root";
        private const string PlayerPassword = "player";

        private static readonly Credentials Root = new Credentials(0);

        public static void Seed(VirtualFileSystem vfs)
        {
            if (vfs is null)
            {
                throw new ArgumentNullException(nameof(vfs));
            }

            vfs.CreateDirectory("/etc", new PermissionMode(0b111_101_101), Root);
            vfs.CreateDirectory("/root", new PermissionMode(0b111_000_000), Root);
            vfs.CreateDirectory("/home", new PermissionMode(0b111_101_101), Root);
            vfs.CreateDirectory(PlayerHome, new PermissionMode(0b111_101_101), Root);
            vfs.Chown(PlayerHome, PlayerUid, PlayerGid, Root);

            WriteFile(vfs, AuthenticationService.PasswdPath, 0b110_100_100,
                $"root:{RootUid}:{RootUid}:/root\n{PlayerName}:{PlayerUid}:{PlayerGid}:{PlayerHome}\n");
            WriteFile(vfs, AuthenticationService.ShadowPath, 0b110_000_000,
                $"root:{PasswordHash.Create(RootPassword)}\n{PlayerName}:{PasswordHash.Create(PlayerPassword)}\n");
        }

        private static void WriteFile(VirtualFileSystem vfs, string path, int mode, string content)
        {
            var stream = vfs.OpenForWrite(path, WriteBehavior.Truncate, new PermissionMode(mode), Root);
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
