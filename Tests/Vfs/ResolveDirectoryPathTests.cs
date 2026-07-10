using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class ResolveDirectoryPathTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static VirtualFileSystem CreateTree()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/a", new PermissionMode(0b111_101_101), Root);
            vfs.CreateDirectory("/a/b", new PermissionMode(0b111_101_101), Root);
            return vfs;
        }

        [Test]
        public void Parent_segments_collapse_to_the_canonical_path()
        {
            var vfs = CreateTree();

            Assert.That(vfs.ResolveDirectoryPath("/a/b/..", Root), Is.EqualTo("/a"));
        }

        [Test]
        public void Symlink_to_a_directory_resolves_to_the_canonical_target()
        {
            var vfs = CreateTree();
            vfs.CreateSymlink("/shortcut", "/a/b", Root);

            Assert.That(vfs.ResolveDirectoryPath("/shortcut", Root), Is.EqualTo("/a/b"));
        }

        [Test]
        public void Root_resolves_to_a_single_slash()
        {
            var vfs = new VirtualFileSystem();

            Assert.That(vfs.ResolveDirectoryPath("/", Root), Is.EqualTo("/"));
        }

        [Test]
        public void Execute_only_directory_can_be_entered()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/gate", new PermissionMode(0b111_001_001), Root);

            Assert.That(vfs.ResolveDirectoryPath("/gate", User(1000)), Is.EqualTo("/gate"));
        }

        [Test]
        public void Trailing_and_duplicate_slashes_are_ignored()
        {
            var vfs = CreateTree();

            Assert.That(vfs.ResolveDirectoryPath("/a//b/", Root), Is.EqualTo("/a/b"));
        }

        [Test]
        public void File_target_yields_ENOTDIR()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", new PermissionMode(0b110_100_100), Root);

            var error = Assert.Throws<VfsException>(() => vfs.ResolveDirectoryPath("/f", Root));

            Assert.That(error.Error, Is.EqualTo(VfsError.ENOTDIR));
        }

        [Test]
        public void Directory_without_execute_bit_yields_EACCES()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/sealed", new PermissionMode(0b110_100_100), Root);

            var error = Assert.Throws<VfsException>(() => vfs.ResolveDirectoryPath("/sealed", User(1000)));

            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Symlink_loop_yields_ELOOP()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateSymlink("/a", "/b", Root);
            vfs.CreateSymlink("/b", "/a", Root);

            var error = Assert.Throws<VfsException>(() => vfs.ResolveDirectoryPath("/a", Root));

            Assert.That(error.Error, Is.EqualTo(VfsError.ELOOP));
        }

        [Test]
        public void Missing_directory_yields_ENOENT()
        {
            var vfs = new VirtualFileSystem();

            var error = Assert.Throws<VfsException>(() => vfs.ResolveDirectoryPath("/absent", Root));

            Assert.That(error.Error, Is.EqualTo(VfsError.ENOENT));
        }
    }
}
