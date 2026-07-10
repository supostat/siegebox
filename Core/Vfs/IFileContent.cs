namespace Siegebox.Vfs
{
    internal interface IFileContent
    {
        int Length { get; }

        int ReadAt(int position, byte[] destination, int offset, int count);

        void WriteAt(int position, byte[] source, int offset, int count);

        void Truncate();

        byte[] Snapshot();
    }
}
