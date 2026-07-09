using System;

namespace Siegebox.Vfs
{
    public sealed class VfsException : Exception
    {
        public VfsException(VfsError error, string path)
            : base($"{error} at '{path}'")
        {
            Error = error;
            Path = path;
        }

        public VfsError Error { get; }

        public string Path { get; }
    }
}
