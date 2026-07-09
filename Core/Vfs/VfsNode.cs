using System;
using System.Collections.Generic;

namespace Siegebox.Vfs
{
    internal sealed class VfsNode
    {
        private const int InitialContentCapacity = 16;

        private byte[] content = Array.Empty<byte>();
        private int contentLength;

        private VfsNode(
            NodeType type,
            string name,
            int ownerUid,
            int groupGid,
            PermissionMode mode,
            Dictionary<string, VfsNode>? children,
            string? symlinkTarget,
            DeviceKind? deviceKind)
        {
            Type = type;
            Name = name;
            OwnerUid = ownerUid;
            GroupGid = groupGid;
            Mode = mode;
            Children = children;
            SymlinkTarget = symlinkTarget;
            DeviceKind = deviceKind;
        }

        public NodeType Type { get; }

        public string Name { get; set; }

        public int OwnerUid { get; set; }

        public int GroupGid { get; set; }

        public PermissionMode Mode { get; set; }

        public VfsNode? Parent { get; set; }

        public Dictionary<string, VfsNode>? Children { get; }

        public string? SymlinkTarget { get; }

        public DeviceKind? DeviceKind { get; }

        public static VfsNode NewFile(string name, int ownerUid, int groupGid, PermissionMode mode)
            => new VfsNode(NodeType.File, name, ownerUid, groupGid, mode, null, null, null);

        public static VfsNode NewDirectory(string name, int ownerUid, int groupGid, PermissionMode mode)
            => new VfsNode(NodeType.Directory, name, ownerUid, groupGid, mode, new Dictionary<string, VfsNode>(StringComparer.Ordinal), null, null);

        public static VfsNode NewSymlink(string name, int ownerUid, int groupGid, PermissionMode mode, string target)
            => new VfsNode(NodeType.Symlink, name, ownerUid, groupGid, mode, null, target, null);

        public static VfsNode NewDevice(string name, int ownerUid, int groupGid, PermissionMode mode, DeviceKind deviceKind)
            => new VfsNode(NodeType.Device, name, ownerUid, groupGid, mode, null, null, deviceKind);

        public int ContentLength => contentLength;

        public void ReadContent(int sourcePosition, byte[] destination, int destinationOffset, int count)
            => Array.Copy(content, sourcePosition, destination, destinationOffset, count);

        public void WriteContent(int destinationPosition, byte[] source, int sourceOffset, int count)
        {
            EnsureCapacity(destinationPosition + count);
            Array.Copy(source, sourceOffset, content, destinationPosition, count);
            if (destinationPosition + count > contentLength)
            {
                contentLength = destinationPosition + count;
            }
        }

        public byte[] SnapshotContent()
        {
            var copy = new byte[contentLength];
            Array.Copy(content, copy, contentLength);
            return copy;
        }

        public VfsNodeSnapshot ToSnapshot()
        {
            return new VfsNodeSnapshot
            {
                Type = Type,
                Name = Name,
                OwnerUid = OwnerUid,
                GroupGid = GroupGid,
                ModeBits = Mode.Bits,
                SymlinkTarget = SymlinkTarget,
                DeviceKind = Type == NodeType.Device ? DeviceKind : null,
                Content = Type == NodeType.File ? SnapshotContent() : null,
                Children = Children is null ? null : SnapshotChildren()
            };
        }

        public static VfsNode FromSnapshot(VfsNodeSnapshot snapshot)
            => FromSnapshot(snapshot, new HashSet<VfsNodeSnapshot>());

        private static VfsNode FromSnapshot(VfsNodeSnapshot snapshot, HashSet<VfsNodeSnapshot> visited)
        {
            if (!visited.Add(snapshot))
            {
                throw new VfsException(VfsError.EINVAL, snapshot.Name);
            }

            var mode = new PermissionMode(snapshot.ModeBits);
            return snapshot.Type switch
            {
                NodeType.File => RebuildFile(snapshot, mode),
                NodeType.Directory => RebuildDirectory(snapshot, mode, visited),
                NodeType.Symlink => NewSymlink(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode, RequireTarget(snapshot)),
                NodeType.Device => NewDevice(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode, RequireDeviceKind(snapshot)),
                _ => throw new VfsException(VfsError.EINVAL, snapshot.Name)
            };
        }

        private void EnsureCapacity(int required)
        {
            if (content.Length >= required)
            {
                return;
            }

            var capacity = content.Length == 0 ? InitialContentCapacity : content.Length * 2;
            while (capacity < required)
            {
                capacity *= 2;
            }

            Array.Resize(ref content, capacity);
        }

        private List<VfsNodeSnapshot> SnapshotChildren()
        {
            var children = new List<VfsNodeSnapshot>(Children!.Count);
            foreach (var child in Children.Values)
            {
                children.Add(child.ToSnapshot());
            }

            return children;
        }

        private static string RequireTarget(VfsNodeSnapshot snapshot)
            => snapshot.SymlinkTarget ?? throw new VfsException(VfsError.EINVAL, snapshot.Name);

        private static DeviceKind RequireDeviceKind(VfsNodeSnapshot snapshot)
            => snapshot.DeviceKind ?? throw new VfsException(VfsError.EINVAL, snapshot.Name);

        private static VfsNode RebuildFile(VfsNodeSnapshot snapshot, PermissionMode mode)
        {
            var node = NewFile(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode);
            var content = snapshot.Content;
            if (content is not null && content.Length > 0)
            {
                node.WriteContent(0, content, 0, content.Length);
            }

            return node;
        }

        private static VfsNode RebuildDirectory(VfsNodeSnapshot snapshot, PermissionMode mode, HashSet<VfsNodeSnapshot> visited)
        {
            var node = NewDirectory(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode);
            if (snapshot.Children is not null)
            {
                foreach (var childSnapshot in snapshot.Children)
                {
                    ValidateChildName(childSnapshot);
                    var child = FromSnapshot(childSnapshot, visited);
                    child.Parent = node;
                    node.Children!.Add(child.Name, child);
                }
            }

            return node;
        }

        private static void ValidateChildName(VfsNodeSnapshot snapshot)
        {
            var name = snapshot.Name;
            if (string.IsNullOrEmpty(name) || name.Contains('/') || name == "." || name == "..")
            {
                throw new VfsException(VfsError.EINVAL, name ?? string.Empty);
            }
        }
    }
}
