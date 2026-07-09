namespace Siegebox.Vfs
{
    public readonly struct VfsEntryInfo
    {
        public VfsEntryInfo(NodeType type, int ownerUid, int groupGid, PermissionMode mode, int size)
        {
            Type = type;
            OwnerUid = ownerUid;
            GroupGid = groupGid;
            Mode = mode;
            Size = size;
        }

        public NodeType Type { get; }

        public int OwnerUid { get; }

        public int GroupGid { get; }

        public PermissionMode Mode { get; }

        public int Size { get; }
    }
}
