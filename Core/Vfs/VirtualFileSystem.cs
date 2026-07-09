using System;
using System.Collections.Generic;

namespace Siegebox.Vfs
{
    public sealed class VirtualFileSystem
    {
        private const int RootUid = 0;
        private const int RootGid = 0;
        private const int RootDirectoryMode = 0b111_101_101;
        private const int DeviceDirectoryMode = 0b111_101_101;
        private const int NullDeviceMode = 0b110_110_110;
        private const int SymlinkMode = 0b111_111_111;

        private readonly VfsNode root;
        private readonly PathResolver resolver;

        public VirtualFileSystem()
        {
            root = VfsNode.NewDirectory(string.Empty, RootUid, RootGid, new PermissionMode(RootDirectoryMode));
            resolver = new PathResolver(root);
            MountStandardDevices();
        }

        private VirtualFileSystem(VfsNode importedRoot)
        {
            root = importedRoot;
            resolver = new PathResolver(root);
        }

        public static VirtualFileSystem Import(VfsNodeSnapshot snapshot)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Type != NodeType.Directory)
            {
                throw new VfsException(VfsError.EINVAL, snapshot.Name);
            }

            return new VirtualFileSystem(VfsNode.FromSnapshot(snapshot));
        }

        public void CreateFile(string path, PermissionMode mode, Credentials credentials)
            => CreateEntry(path, credentials, (parent, name) => VfsNode.NewFile(name, credentials.Uid, parent.GroupGid, mode));

        public void CreateDirectory(string path, PermissionMode mode, Credentials credentials)
            => CreateEntry(path, credentials, (parent, name) => VfsNode.NewDirectory(name, credentials.Uid, parent.GroupGid, mode));

        public void CreateSymlink(string path, string target, Credentials credentials)
        {
            if (target is null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            CreateEntry(path, credentials, (parent, name) => VfsNode.NewSymlink(name, credentials.Uid, parent.GroupGid, new PermissionMode(SymlinkMode), target));
        }

        public void CreateDevice(string path, DeviceKind kind, PermissionMode mode, Credentials credentials)
            => CreateEntry(path, credentials, (parent, name) => VfsNode.NewDevice(name, credentials.Uid, parent.GroupGid, mode, kind));

        public IByteStream Open(string path, OpenMode mode, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, AccessFor(mode), true);
            return node.Type switch
            {
                NodeType.Directory => throw new VfsException(VfsError.EISDIR, path),
                NodeType.Device => OpenDevice(node),
                NodeType.File => new FileStream(node.Content!, mode),
                _ => throw new VfsException(VfsError.EINVAL, path)
            };
        }

        public void Delete(string path, Credentials credentials)
        {
            var (parent, name) = resolver.ResolveParent(path, credentials);
            resolver.RequireAccess(parent, credentials, PermissionMode.WriteBit, path);
            var node = Lookup(parent, name, path);
            if (node.Type == NodeType.Directory && node.Children!.Count > 0)
            {
                throw new VfsException(VfsError.ENOTEMPTY, path);
            }

            parent.Children!.Remove(name);
            node.Parent = null;
        }

        public void Move(string sourcePath, string destinationPath, Credentials credentials)
        {
            var (sourceParent, sourceName) = resolver.ResolveParent(sourcePath, credentials);
            resolver.RequireAccess(sourceParent, credentials, PermissionMode.WriteBit, sourcePath);
            var node = Lookup(sourceParent, sourceName, sourcePath);

            var (destinationParent, destinationName) = resolver.ResolveParent(destinationPath, credentials);
            resolver.RequireAccess(destinationParent, credentials, PermissionMode.WriteBit, destinationPath);
            GuardNotIntoOwnSubtree(node, destinationParent, destinationName, destinationPath);
            GuardNotExists(destinationParent, destinationName, destinationPath);

            sourceParent.Children!.Remove(sourceName);
            node.Name = destinationName;
            Attach(destinationParent, node);
        }

        public void Copy(string sourcePath, string destinationPath, Credentials credentials)
        {
            var source = resolver.Resolve(sourcePath, credentials, PermissionMode.ReadBit, true);
            if (source.Type == NodeType.Directory)
            {
                throw new VfsException(VfsError.EISDIR, sourcePath);
            }

            var content = source.Content?.Snapshot() ?? Array.Empty<byte>();
            var (destinationParent, destinationName) = resolver.ResolveParent(destinationPath, credentials);
            resolver.RequireAccess(destinationParent, credentials, PermissionMode.WriteBit, destinationPath);
            GuardNotExists(destinationParent, destinationName, destinationPath);

            var copy = VfsNode.NewFile(destinationName, credentials.Uid, destinationParent.GroupGid, source.Mode, new FileContent(content));
            Attach(destinationParent, copy);
        }

        public IReadOnlyList<string> List(string path, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, PermissionMode.ReadBit, true);
            if (node.Type != NodeType.Directory)
            {
                throw new VfsException(VfsError.ENOTDIR, path);
            }

            var names = new List<string>(node.Children!.Keys);
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public VfsEntryInfo Stat(string path, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, 0, false);
            return new VfsEntryInfo(node.Type, node.OwnerUid, node.GroupGid, node.Mode, SizeOf(node));
        }

        public void Chmod(string path, PermissionMode mode, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, 0, true);
            RequireOwnerOrRoot(node, credentials, path);
            node.Mode = mode;
        }

        public void Chown(string path, int ownerUid, int groupGid, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, 0, true);
            if (!credentials.IsRoot)
            {
                throw new VfsException(VfsError.EPERM, path);
            }

            node.OwnerUid = ownerUid;
            node.GroupGid = groupGid;
        }

        public VfsNodeSnapshot Export() => root.ToSnapshot();

        private void MountStandardDevices()
        {
            var rootCredentials = new Credentials(RootUid, RootGid);
            CreateDirectory("/dev", new PermissionMode(DeviceDirectoryMode), rootCredentials);
            CreateDevice("/dev/null", DeviceKind.Null, new PermissionMode(NullDeviceMode), rootCredentials);
        }

        private static IByteStream OpenDevice(VfsNode node) => node.DeviceKind switch
        {
            DeviceKind.Null => new NullStream(),
            _ => throw new VfsException(VfsError.EINVAL, node.Name)
        };

        private void CreateEntry(string path, Credentials credentials, Func<VfsNode, string, VfsNode> nodeFactory)
        {
            var (parent, name) = resolver.ResolveParent(path, credentials);
            resolver.RequireAccess(parent, credentials, PermissionMode.WriteBit, path);
            GuardNotExists(parent, name, path);
            Attach(parent, nodeFactory(parent, name));
        }

        private static void Attach(VfsNode parent, VfsNode child)
        {
            child.Parent = parent;
            parent.Children!.Add(child.Name, child);
        }

        private static VfsNode Lookup(VfsNode parent, string name, string path)
        {
            if (parent.Children!.TryGetValue(name, out var node))
            {
                return node;
            }

            throw new VfsException(VfsError.ENOENT, path);
        }

        private static void GuardNotExists(VfsNode parent, string name, string path)
        {
            if (parent.Children!.ContainsKey(name))
            {
                throw new VfsException(VfsError.EEXIST, path);
            }
        }

        private static void GuardNotIntoOwnSubtree(VfsNode node, VfsNode destinationParent, string destinationName, string destinationPath)
        {
            if (node.Type != NodeType.Directory)
            {
                return;
            }

            for (VfsNode? ancestor = destinationParent; ancestor is not null; ancestor = ancestor.Parent)
            {
                if (ancestor == node)
                {
                    throw new VfsException(VfsError.EINVAL, destinationPath);
                }
            }

            if (destinationParent == node.Parent && destinationName == node.Name)
            {
                throw new VfsException(VfsError.EINVAL, destinationPath);
            }
        }

        private static void RequireOwnerOrRoot(VfsNode node, Credentials credentials, string path)
        {
            if (credentials.IsRoot || node.OwnerUid == credentials.Uid)
            {
                return;
            }

            throw new VfsException(VfsError.EPERM, path);
        }

        private static int SizeOf(VfsNode node) => node.Type switch
        {
            NodeType.File => node.Content!.Length,
            NodeType.Symlink => node.SymlinkTarget!.Length,
            NodeType.Directory => node.Children!.Count,
            _ => 0
        };

        private static int AccessFor(OpenMode mode) => mode switch
        {
            OpenMode.Read => PermissionMode.ReadBit,
            OpenMode.Write => PermissionMode.WriteBit,
            OpenMode.ReadWrite => PermissionMode.ReadBit | PermissionMode.WriteBit,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
