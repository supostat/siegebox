using System.Text;
using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class SymlinkResolutionTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        [Test]
        public void Symlink_resolves_to_its_target_content()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/target", Mode(0b110_110_110), Root);
            Write(vfs, "/target", "payload");
            vfs.CreateSymlink("/link", "/target", Root);
            Assert.That(Read(vfs, "/link"), Is.EqualTo("payload"));
        }

        [Test]
        public void Relative_symlink_resolves_against_its_own_directory()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/dir", Mode(0b111_101_101), Root);
            vfs.CreateFile("/dir/target", Mode(0b110_110_110), Root);
            Write(vfs, "/dir/target", "here");
            vfs.CreateSymlink("/dir/link", "target", Root);
            Assert.That(Read(vfs, "/dir/link"), Is.EqualTo("here"));
        }

        [Test]
        public void Target_permissions_govern_access_not_the_symlinks_own_permissions()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/target", Mode(0b110_000_000), Root);
            vfs.Chown("/target", 100, 500, Root);
            vfs.CreateSymlink("/link", "/target", Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/link", OpenMode.Read, User(200)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Symlink_cycle_reaches_the_depth_limit_and_yields_ELOOP()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateSymlink("/a", "/b", Root);
            vfs.CreateSymlink("/b", "/a", Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/a", OpenMode.Read, Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ELOOP));
        }

        [Test]
        public void Dangling_symlink_yields_ENOENT_when_followed()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateSymlink("/link", "/missing", Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/link", OpenMode.Read, Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ENOENT));
        }

        [Test]
        public void Total_symlink_follow_budget_is_bounded_across_sibling_links()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/base", Mode(0b111_101_101), Root);
            vfs.CreateSymlink("/s0", "/base", Root);
            for (var index = 1; index <= 5; index++)
            {
                vfs.CreateSymlink($"/s{index}", $"/s{index - 1}/../s{index - 1}", Root);
            }

            var error = Assert.Throws<VfsException>(() => vfs.List("/s5", Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ELOOP));
        }

        private static void Write(VirtualFileSystem vfs, string path, string text)
        {
            var stream = vfs.Open(path, OpenMode.Write, Root);
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
        }

        private static string Read(VirtualFileSystem vfs, string path)
        {
            var stream = vfs.Open(path, OpenMode.Read, Root);
            var buffer = new byte[256];
            var result = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
    }
}
