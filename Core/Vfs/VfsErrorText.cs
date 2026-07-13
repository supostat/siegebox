namespace Siegebox.Vfs
{
    internal static class VfsErrorText
    {
        public static string MessageFor(VfsError error) => error switch
        {
            VfsError.ENOENT => "No such file or directory",
            VfsError.EACCES => "Permission denied",
            VfsError.ELOOP => "Too many levels of symbolic links",
            VfsError.ENOTDIR => "Not a directory",
            VfsError.EEXIST => "File exists",
            VfsError.EISDIR => "Is a directory",
            VfsError.ENOTEMPTY => "Directory not empty",
            VfsError.EPERM => "Operation not permitted",
            _ => "Invalid argument"
        };
    }
}
