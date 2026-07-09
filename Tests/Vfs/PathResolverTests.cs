using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class PathResolverTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        [Test]
        public void Missing_path_yields_ENOENT()
        {
            var vfs = new VirtualFileSystem();
            var error = Assert.Throws<VfsException>(() => vfs.Open("/absent", OpenMode.Read, Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ENOENT));
        }

        [Test]
        public void Non_directory_component_in_path_yields_ENOTDIR()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/file", Mode(0b110_100_100), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/file/inner", OpenMode.Read, Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ENOTDIR));
        }

        [Test]
        public void Traverse_denied_without_execute_on_intermediate_directory_yields_EACCES()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/private", Mode(0b111_000_000), Root);
            vfs.CreateFile("/private/secret", Mode(0b110_110_110), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/private/secret", OpenMode.Read, User(100)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Target_permission_bits_are_checked_against_the_target_node()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/shared", Mode(0b111_101_101), Root);
            vfs.CreateFile("/shared/private", Mode(0b110_000_000), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/shared/private", OpenMode.Read, User(100)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Root_short_circuits_traverse_and_target_permission_checks()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/vault", Mode(0), Root);
            vfs.CreateFile("/vault/data", Mode(0), Root);
            Assert.That(() => vfs.List("/vault", Root), Throws.Nothing);
            var denied = Assert.Throws<VfsException>(() => vfs.List("/vault", User(100)));
            Assert.That(denied.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Stat_needs_only_traverse_and_ignores_the_target_permission_bits()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/dir", Mode(0b111_101_101), Root);
            vfs.CreateFile("/dir/target", Mode(0), Root);
            vfs.Chown("/dir/target", 100, 500, Root);
            var info = vfs.Stat("/dir/target", User(200, 999));
            Assert.That(info.Type, Is.EqualTo(NodeType.File));
        }

        [Test]
        public void Stat_is_denied_when_an_intermediate_directory_lacks_execute()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/locked", Mode(0b111_000_000), Root);
            vfs.CreateFile("/locked/target", Mode(0b110_110_110), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Stat("/locked/target", User(200)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Dot_and_dotdot_segments_resolve_relative_to_the_current_directory()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/dir", Mode(0b111_101_101), Root);
            vfs.CreateFile("/dir/inner", Mode(0b110_110_110), Root);
            vfs.CreateDirectory("/sibling", Mode(0b111_101_101), Root);
            Assert.That(vfs.Stat("/dir/../sibling", Root).Type, Is.EqualTo(NodeType.Directory));
            Assert.That(vfs.Stat("/dir/./inner", Root).Type, Is.EqualTo(NodeType.File));
        }
    }
}
