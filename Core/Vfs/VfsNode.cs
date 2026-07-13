using System;
using System.Collections.Generic;

namespace Siegebox.Vfs
{
    internal sealed class VfsNode
    {
        private const int MaxImportDepth = 64;

        private VfsNode(
            NodeType type,
            string name,
            int ownerUid,
            int groupGid,
            PermissionMode mode,
            Dictionary<string, VfsNode>? children,
            string? symlinkTarget,
            DeviceKind? deviceKind,
            IFileContent? content)
        {
            Type = type;
            Name = name;
            OwnerUid = ownerUid;
            GroupGid = groupGid;
            Mode = mode;
            Children = children;
            SymlinkTarget = symlinkTarget;
            DeviceKind = deviceKind;
            Content = content;
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

        public IFileContent? Content { get; }

        public static VfsNode NewFile(string name, int ownerUid, int groupGid, PermissionMode mode)
            => NewFile(name, ownerUid, groupGid, mode, new FileContent());

        public static VfsNode NewFile(string name, int ownerUid, int groupGid, PermissionMode mode, IFileContent content)
            => new VfsNode(NodeType.File, name, ownerUid, groupGid, mode, null, null, null, content);

        public static VfsNode NewDirectory(string name, int ownerUid, int groupGid, PermissionMode mode)
            => new VfsNode(NodeType.Directory, name, ownerUid, groupGid, mode, new Dictionary<string, VfsNode>(StringComparer.Ordinal), null, null, null);

        public static VfsNode NewSymlink(string name, int ownerUid, int groupGid, PermissionMode mode, string target)
            => new VfsNode(NodeType.Symlink, name, ownerUid, groupGid, mode, null, target, null, null);

        public static VfsNode NewDevice(string name, int ownerUid, int groupGid, PermissionMode mode, DeviceKind deviceKind)
            => new VfsNode(NodeType.Device, name, ownerUid, groupGid, mode, null, null, deviceKind, null);

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
                Content = Type == NodeType.File ? Content!.Snapshot() : null,
                Children = Children is null ? null : SnapshotChildren()
            };
        }

        public static VfsNode FromSnapshot(VfsNodeSnapshot snapshot)
            => FromSnapshot(snapshot, new HashSet<VfsNodeSnapshot>(), 0);

        private static VfsNode FromSnapshot(VfsNodeSnapshot snapshot, HashSet<VfsNodeSnapshot> visited, int depth)
        {
            if (depth > MaxImportDepth)
            {
                throw new VfsException(VfsError.EINVAL, snapshot.Name);
            }

            if (!visited.Add(snapshot))
            {
                throw new VfsException(VfsError.EINVAL, snapshot.Name);
            }

            var mode = ParseMode(snapshot);
            return snapshot.Type switch
            {
                NodeType.File => RebuildFile(snapshot, mode),
                NodeType.Directory => RebuildDirectory(snapshot, mode, visited, depth),
                NodeType.Symlink => NewSymlink(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode, RequireTarget(snapshot)),
                NodeType.Device => NewDevice(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode, RequireDeviceKind(snapshot)),
                _ => throw new VfsException(VfsError.EINVAL, snapshot.Name)
            };
        }

        private List<VfsNodeSnapshot> SnapshotChildren()
        {
            var orderedNames = new List<string>(Children!.Keys);
            orderedNames.Sort(StringComparer.Ordinal);
            var children = new List<VfsNodeSnapshot>(orderedNames.Count);
            foreach (var name in orderedNames)
            {
                children.Add(Children[name].ToSnapshot());
            }

            return children;
        }

        private static PermissionMode ParseMode(VfsNodeSnapshot snapshot)
        {
            try
            {
                return new PermissionMode(snapshot.ModeBits);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw new VfsException(VfsError.EINVAL, snapshot.Name);
            }
        }

        private static string RequireTarget(VfsNodeSnapshot snapshot)
            => snapshot.SymlinkTarget ?? throw new VfsException(VfsError.EINVAL, snapshot.Name);

        private static DeviceKind RequireDeviceKind(VfsNodeSnapshot snapshot)
            => snapshot.DeviceKind ?? throw new VfsException(VfsError.EINVAL, snapshot.Name);

        private static VfsNode RebuildFile(VfsNodeSnapshot snapshot, PermissionMode mode)
        {
            var initialBytes = snapshot.Content ?? Array.Empty<byte>();
            return NewFile(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode, new FileContent(initialBytes));
        }

        private static VfsNode RebuildDirectory(VfsNodeSnapshot snapshot, PermissionMode mode, HashSet<VfsNodeSnapshot> visited, int depth)
        {
            var node = NewDirectory(snapshot.Name, snapshot.OwnerUid, snapshot.GroupGid, mode);
            if (snapshot.Children is not null)
            {
                foreach (var childSnapshot in snapshot.Children)
                {
                    if (childSnapshot is null)
                    {
                        throw new VfsException(VfsError.EINVAL, string.Empty);
                    }

                    ValidateChildName(childSnapshot);
                    if (node.Children!.ContainsKey(childSnapshot.Name))
                    {
                        throw new VfsException(VfsError.EINVAL, childSnapshot.Name);
                    }

                    var child = FromSnapshot(childSnapshot, visited, depth + 1);
                    child.Parent = node;
                    node.Children.Add(child.Name, child);
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

            foreach (var character in name)
            {
                if (char.IsControl(character))
                {
                    throw new VfsException(VfsError.EINVAL, name);
                }
            }
        }
    }
}
