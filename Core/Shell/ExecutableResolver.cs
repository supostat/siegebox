using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// The single kernel-side source of setuid elevation. On spawn it stats /usr/bin/&lt;name&gt;
    /// under trusted root credentials; when the file is a setuid executable the calling session
    /// may execute, it returns the file owner's identity as the process's EFFECTIVE credentials
    /// (the <see cref="PipelineAssembler"/> pairs it with the session as the real identity). An
    /// already-root session is never demoted, and a missing or non-qualifying file yields null,
    /// so a stock command with no /usr/bin file runs unchanged.
    /// </summary>
    internal sealed class ExecutableResolver
    {
        private const string BinDirectory = "/usr/bin/";

        private static readonly Credentials TrustedRoot = new Credentials(0);

        private readonly VirtualFileSystem vfs;

        public ExecutableResolver(VirtualFileSystem vfs)
        {
            this.vfs = vfs;
        }

        public Credentials? Resolve(string commandName, Credentials session)
        {
            if (session.IsRoot)
            {
                return null;
            }

            VfsEntryInfo info;
            try
            {
                // Deliberately an lstat (no final-symlink follow): resolving the target under
                // trusted root would let a session-owned symlink point at any setuid file and
                // borrow its identity, so elevation is read only from the /usr/bin file itself.
                info = vfs.Stat(BinDirectory + commandName, TrustedRoot);
            }
            catch (VfsException)
            {
                return null;
            }

            if (info.Type != NodeType.File || !info.Mode.SetUid || !SessionCanExecute(info, session))
            {
                return null;
            }

            return new Credentials(info.OwnerUid, info.GroupGid);
        }

        private static bool SessionCanExecute(VfsEntryInfo info, Credentials session)
        {
            var permissionClass = info.OwnerUid == session.Uid
                ? info.Mode.OwnerRwx
                : session.InGroup(info.GroupGid) ? info.Mode.GroupRwx : info.Mode.OtherRwx;
            return (permissionClass & PermissionMode.ExecuteBit) != 0;
        }
    }
}
