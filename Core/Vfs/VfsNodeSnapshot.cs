using System.Collections.Generic;

namespace Siegebox.Vfs
{
    public sealed class VfsNodeSnapshot
    {
        public NodeType Type { get; set; }

        public string Name { get; set; } = string.Empty;

        public int OwnerUid { get; set; }

        public int GroupGid { get; set; }

        public int ModeBits { get; set; }

        public byte[]? Content { get; set; }

        public string? SymlinkTarget { get; set; }

        public DeviceKind? DeviceKind { get; set; }

        public List<VfsNodeSnapshot>? Children { get; set; }
    }
}
