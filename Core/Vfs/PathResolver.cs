using System;
using System.Collections.Generic;

namespace Siegebox.Vfs
{
    internal sealed class PathResolver
    {
        private const int MaxSymlinkDepth = 40;
        private const string ParentSegment = "..";

        private readonly VfsNode root;

        public PathResolver(VfsNode root)
        {
            this.root = root;
        }

        public VfsNode Resolve(string path, Credentials credentials, int requiredAccess, bool followFinalSymlink)
        {
            RequireArguments(path, credentials);
            var segments = SplitSegments(path);
            var followCount = 0;
            var target = WalkFrom(root, segments, segments.Count, credentials, path, followFinalSymlink, ref followCount);
            CheckAccess(target, credentials, requiredAccess, path);
            return target;
        }

        public (VfsNode Parent, string Name) ResolveParent(string path, Credentials credentials)
        {
            RequireArguments(path, credentials);
            var segments = SplitSegments(path);
            if (segments.Count == 0)
            {
                throw new VfsException(VfsError.EINVAL, path);
            }

            var name = segments[segments.Count - 1];
            var followCount = 0;
            var parent = WalkFrom(root, segments, segments.Count - 1, credentials, path, true, ref followCount);
            if (parent.Type != NodeType.Directory)
            {
                throw new VfsException(VfsError.ENOTDIR, path);
            }

            CheckTraverse(parent, credentials, path);
            return (parent, name);
        }

        public void RequireAccess(VfsNode node, Credentials credentials, int requiredAccess, string path)
            => CheckAccess(node, credentials, requiredAccess, path);

        public string CanonicalPathOf(VfsNode node)
        {
            if (node.Parent is null)
            {
                return "/";
            }

            var segments = new List<string>();
            for (VfsNode? current = node; current!.Parent is not null; current = current.Parent)
            {
                segments.Add(current.Name);
            }

            segments.Reverse();
            return "/" + string.Join("/", segments);
        }

        /// <summary>
        /// Follows the final path component through any symlink chain (existing or dangling)
        /// and returns the canonical path the chain points at, so create-on-write targets
        /// the symlink's destination instead of colliding with the symlink node itself.
        /// </summary>
        public string FinalTargetPathOf(string path, Credentials credentials)
        {
            var currentPath = path;
            var followCount = 0;
            while (true)
            {
                var (parent, name) = ResolveParent(currentPath, credentials);
                if (!parent.Children!.TryGetValue(name, out var child) || child.Type != NodeType.Symlink)
                {
                    return JoinCanonical(CanonicalPathOf(parent), name);
                }

                followCount++;
                if (followCount > MaxSymlinkDepth)
                {
                    throw new VfsException(VfsError.ELOOP, path);
                }

                var target = child.SymlinkTarget!;
                currentPath = target.Length > 0 && target[0] == '/'
                    ? target
                    : JoinCanonical(CanonicalPathOf(parent), target);
            }
        }

        private VfsNode WalkFrom(VfsNode start, List<string> segments, int count, Credentials credentials, string path, bool followLast, ref int followCount)
        {
            var current = start;
            for (var index = 0; index < count; index++)
            {
                if (current.Type != NodeType.Directory)
                {
                    throw new VfsException(VfsError.ENOTDIR, path);
                }

                CheckTraverse(current, credentials, path);
                var name = segments[index];
                if (name == ParentSegment)
                {
                    current = current.Parent ?? root;
                    continue;
                }

                var child = Descend(current, name, path);
                if (child.Type == NodeType.Symlink && (index < count - 1 || followLast))
                {
                    child = ResolveSymlink(child, credentials, path, ref followCount);
                }

                current = child;
            }

            return current;
        }

        private VfsNode ResolveSymlink(VfsNode link, Credentials credentials, string path, ref int followCount)
        {
            followCount++;
            if (followCount > MaxSymlinkDepth)
            {
                throw new VfsException(VfsError.ELOOP, path);
            }

            var target = link.SymlinkTarget!;
            var start = target.Length > 0 && target[0] == '/' ? root : link.Parent ?? root;
            var segments = SplitSegments(target);
            return WalkFrom(start, segments, segments.Count, credentials, path, true, ref followCount);
        }

        private static string JoinCanonical(string directoryPath, string name)
            => directoryPath == "/" ? "/" + name : directoryPath + "/" + name;

        private static VfsNode Descend(VfsNode directory, string name, string path)
        {
            if (directory.Children!.TryGetValue(name, out var child))
            {
                return child;
            }

            throw new VfsException(VfsError.ENOENT, path);
        }

        private static void CheckTraverse(VfsNode directory, Credentials credentials, string path)
        {
            if (credentials.IsRoot)
            {
                return;
            }

            if ((PermissionClass(directory, credentials) & PermissionMode.ExecuteBit) != PermissionMode.ExecuteBit)
            {
                throw new VfsException(VfsError.EACCES, path);
            }
        }

        private static void CheckAccess(VfsNode node, Credentials credentials, int requiredAccess, string path)
        {
            if (credentials.IsRoot)
            {
                return;
            }

            if ((PermissionClass(node, credentials) & requiredAccess) != requiredAccess)
            {
                throw new VfsException(VfsError.EACCES, path);
            }
        }

        private static int PermissionClass(VfsNode node, Credentials credentials)
        {
            if (node.OwnerUid == credentials.Uid)
            {
                return node.Mode.OwnerRwx;
            }

            if (credentials.InGroup(node.GroupGid))
            {
                return node.Mode.GroupRwx;
            }

            return node.Mode.OtherRwx;
        }

        private static List<string> SplitSegments(string path)
        {
            var segments = new List<string>();
            foreach (var segment in path.Split('/'))
            {
                if (segment.Length == 0 || segment == ".")
                {
                    continue;
                }

                segments.Add(segment);
            }

            return segments;
        }

        private static void RequireArguments(string path, Credentials credentials)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (credentials is null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }
        }
    }
}
