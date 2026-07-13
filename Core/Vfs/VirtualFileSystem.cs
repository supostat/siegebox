using System;
using System.Collections.Generic;
using Siegebox.Events;

namespace Siegebox.Vfs
{
    /// <summary>
    /// The complete-mediation door to the permissioned tree: every open goes through the
    /// resolver under the caller's credentials. Publishes a <see cref="KernelEvent.FileOpened"/>
    /// hook on the injected <see cref="EventBus"/> after a successful <see cref="Open"/> /
    /// <see cref="OpenForWrite"/> (never on a denied open, which grants no capability).
    /// </summary>
    public sealed class VirtualFileSystem
    {
        private const int RootUid = 0;
        private const int RootGid = 0;
        private const int RootDirectoryMode = 0b111_101_101;
        private const int DeviceDirectoryMode = 0b111_101_101;
        private const int NullDeviceMode = 0b110_110_110;
        private const int SymlinkMode = 0b111_111_111;
        private const string ReadAccessName = "read";
        private const string WriteAccessName = "write";
        private const string ReadWriteAccessName = "readwrite";

        private readonly VfsNode root;
        private readonly PathResolver resolver;
        private readonly EventBus events;

        public VirtualFileSystem(EventBus? events = null)
        {
            this.events = events ?? new EventBus();
            root = VfsNode.NewDirectory(string.Empty, RootUid, RootGid, new PermissionMode(RootDirectoryMode));
            resolver = new PathResolver(root);
            MountStandardDevices();
        }

        private VirtualFileSystem(VfsNode importedRoot, EventBus? events)
        {
            this.events = events ?? new EventBus();
            root = importedRoot;
            resolver = new PathResolver(root);
        }

        public static VirtualFileSystem Import(VfsNodeSnapshot snapshot, EventBus? events = null)
        {
            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (snapshot.Type != NodeType.Directory)
            {
                throw new VfsException(VfsError.EINVAL, snapshot.Name);
            }

            return new VirtualFileSystem(VfsNode.FromSnapshot(snapshot), events);
        }

        public void CreateFile(string path, PermissionMode mode, Credentials credentials)
        {
            var effectiveMode = credentials.IsRoot ? mode : mode.WithSetUid(false);
            CreateEntry(path, credentials, (parent, name) => VfsNode.NewFile(name, credentials.Uid, parent.GroupGid, effectiveMode));
        }

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
            var stream = node.Type switch
            {
                NodeType.Directory => throw new VfsException(VfsError.EISDIR, path),
                NodeType.Device => OpenDevice(node),
                NodeType.File => OpenFileForAccess(node, mode, credentials),
                _ => throw new VfsException(VfsError.EINVAL, path)
            };
            events.Publish(KernelEvent.FileOpened(path, credentials.Uid, AccessNameFor(mode)));
            return stream;
        }

        /// <summary>
        /// Opens a file for writing, creating it when missing. A dangling final symlink is
        /// followed BEFORE creation, so the write creates the symlink's target (POSIX).
        /// </summary>
        public IByteStream OpenForWrite(string path, WriteBehavior behavior, PermissionMode createMode, Credentials credentials)
        {
            IByteStream stream;
            try
            {
                stream = OpenExistingForWrite(path, behavior, credentials);
            }
            catch (VfsException error) when (error.Error == VfsError.ENOENT)
            {
                var targetPath = resolver.FinalTargetPathOf(path, credentials);
                CreateFile(targetPath, createMode, credentials);
                stream = OpenExistingForWrite(targetPath, behavior, credentials);
            }

            events.Publish(KernelEvent.FileOpened(path, credentials.Uid, WriteAccessName));
            return stream;
        }

        /// <summary>Resolves to an enterable directory; returns its canonical absolute path.</summary>
        public string ResolveDirectoryPath(string path, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, PermissionMode.ExecuteBit, true);
            if (node.Type != NodeType.Directory)
            {
                throw new VfsException(VfsError.ENOTDIR, path);
            }

            return resolver.CanonicalPathOf(node);
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

            var copyMode = credentials.IsRoot ? source.Mode : source.Mode.WithSetUid(false);
            var copy = VfsNode.NewFile(destinationName, credentials.Uid, destinationParent.GroupGid, copyMode, new FileContent(content));
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
            if (mode.SetUid && !node.Mode.SetUid && !credentials.IsRoot)
            {
                throw new VfsException(VfsError.EPERM, path);
            }

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

        private IByteStream OpenExistingForWrite(string path, WriteBehavior behavior, Credentials credentials)
        {
            var node = resolver.Resolve(path, credentials, PermissionMode.WriteBit, true);
            switch (node.Type)
            {
                case NodeType.Directory:
                    throw new VfsException(VfsError.EISDIR, path);
                case NodeType.Device:
                    return OpenDevice(node);
                case NodeType.File:
                    DropSetuidOnNonRootWrite(node, credentials);
                    if (behavior == WriteBehavior.Truncate)
                    {
                        node.Content!.Truncate();
                    }

                    return new FileStream(node.Content!, OpenMode.Write, behavior == WriteBehavior.Append);
                default:
                    throw new VfsException(VfsError.EINVAL, path);
            }
        }

        private static IByteStream OpenFileForAccess(VfsNode node, OpenMode mode, Credentials credentials)
        {
            if (mode == OpenMode.Write || mode == OpenMode.ReadWrite)
            {
                DropSetuidOnNonRootWrite(node, credentials);
            }

            return new FileStream(node.Content!, mode);
        }

        /// <summary>
        /// POSIX drop-on-write: a non-root write to an existing setuid file clears the bit. The
        /// single place both write doors (Open write modes and OpenForWrite) enforce it, so the
        /// invariant cannot be bypassed by picking one door over the other. Runs only after the
        /// resolver has already granted write access.
        /// </summary>
        private static void DropSetuidOnNonRootWrite(VfsNode node, Credentials credentials)
        {
            if (!credentials.IsRoot && node.Mode.SetUid)
            {
                node.Mode = node.Mode.WithSetUid(false);
            }
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

        private static string AccessNameFor(OpenMode mode) => mode switch
        {
            OpenMode.Read => ReadAccessName,
            OpenMode.Write => WriteAccessName,
            OpenMode.ReadWrite => ReadWriteAccessName,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
