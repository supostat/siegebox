namespace Siegebox.Unity
{
    /// <summary>One row the file manager shows: a name, whether it opens as a directory, and a size.</summary>
    public readonly struct FileManagerEntry
    {
        public FileManagerEntry(string name, bool isDirectory, int size)
        {
            Name = name;
            IsDirectory = isDirectory;
            Size = size;
        }

        public string Name { get; }

        public bool IsDirectory { get; }

        public int Size { get; }
    }
}
