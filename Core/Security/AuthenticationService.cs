using System;
using System.IO;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Security
{
    /// <summary>
    /// The trusted door to the user db in the VFS. Reads /etc/passwd (public identities) and
    /// /etc/shadow (password hashes, 0600) through the resolver. It reads shadow under root
    /// credentials on purpose — this is the one auditable place that holds that capability,
    /// the setuid-root equivalent of the su mechanism; nothing else reads shadow, so an
    /// ordinary process or mod still gets EACCES. Reads fail closed: a missing db or a
    /// bad password is a resolve-miss / a false, never an escalation or a thrown auth bypass.
    /// </summary>
    public sealed class AuthenticationService
    {
        public const string PasswdPath = "/etc/passwd";
        public const string ShadowPath = "/etc/shadow";

        private static readonly Credentials Root = new Credentials(0);

        private readonly VirtualFileSystem vfs;

        public AuthenticationService(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public bool TryResolveByName(string name, out UserRecord record)
        {
            record = null!;
            return TryReadPasswd(out var database) && database.TryGetByName(name, out record);
        }

        public bool TryResolveByUid(int uid, out UserRecord record)
        {
            record = null!;
            return TryReadPasswd(out var database) && database.TryGetByUid(uid, out record);
        }

        public bool Authenticate(string name, string password)
        {
            if (name is null || password is null)
            {
                return false;
            }

            return TryReadShadow(out var shadow)
                   && shadow.TryGetHash(name, out var hash)
                   && PasswordHash.Verify(password, hash);
        }

        private bool TryReadPasswd(out UserDatabase database)
        {
            database = null!;
            if (!TryReadFile(PasswdPath, out var content))
            {
                return false;
            }

            try
            {
                database = UserDatabase.Parse(content);
                return true;
            }
            catch (UserDatabaseException)
            {
                return false;
            }
        }

        private bool TryReadShadow(out ShadowTable shadow)
        {
            shadow = null!;
            if (!TryReadFile(ShadowPath, out var content))
            {
                return false;
            }

            try
            {
                shadow = ShadowTable.Parse(content);
                return true;
            }
            catch (UserDatabaseException)
            {
                return false;
            }
        }

        private bool TryReadFile(string path, out string content)
        {
            content = string.Empty;
            IByteStream stream;
            try
            {
                stream = vfs.Open(path, OpenMode.Read, Root);
            }
            catch (VfsException)
            {
                return false;
            }

            try
            {
                content = ReadAllText(stream);
                return true;
            }
            finally
            {
                stream.CloseRead();
            }
        }

        private static string ReadAllText(IByteStream stream)
        {
            var collected = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                var result = stream.Read(chunk, 0, chunk.Length);
                if (result.Status != StreamStatus.Ok)
                {
                    return Encoding.UTF8.GetString(collected.ToArray());
                }

                collected.Write(chunk, 0, result.Count);
            }
        }
    }
}
