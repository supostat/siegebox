using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class PermissionCheckTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        private static VirtualFileSystem FileOwnedBy(int ownerUid, int groupGid, int modeBits)
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(modeBits), Root);
            vfs.Chown("/f", ownerUid, groupGid, Root);
            return vfs;
        }

        private static void FillAsRoot(VirtualFileSystem vfs, string path, byte[] content)
        {
            var stream = vfs.Open(path, OpenMode.Write, Root);
            stream.Write(content, 0, content.Length);
            stream.CloseWrite();
        }

        private static void AssertReadsOneByte(IByteStream stream)
        {
            var read = stream.Read(new byte[1], 0, 1);
            Assert.That(read.Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(read.Count, Is.EqualTo(1));
        }

        [Test]
        public void Foreign_uid_is_denied_with_EACCES()
        {
            var vfs = FileOwnedBy(100, 500, 0b110_000_000);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/f", OpenMode.Read, User(200, 500)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Owner_bits_grant_owner_access()
        {
            var vfs = FileOwnedBy(100, 500, 0b110_000_000);
            var owner = User(100, 999);
            var writer = vfs.Open("/f", OpenMode.Write, owner);
            Assert.That(writer.Write(new byte[] { 42 }, 0, 1).Status, Is.EqualTo(StreamStatus.Ok));
            AssertReadsOneByte(vfs.Open("/f", OpenMode.Read, owner));
        }

        [Test]
        public void Group_bits_grant_access_to_a_group_member()
        {
            var vfs = FileOwnedBy(100, 500, 0b000_100_000);
            FillAsRoot(vfs, "/f", new byte[] { 7 });
            AssertReadsOneByte(vfs.Open("/f", OpenMode.Read, User(200, 500)));
        }

        [Test]
        public void Other_bits_apply_when_not_owner_and_not_in_group()
        {
            var vfs = FileOwnedBy(100, 500, 0b000_000_100);
            FillAsRoot(vfs, "/f", new byte[] { 9 });
            AssertReadsOneByte(vfs.Open("/f", OpenMode.Read, User(300, 999)));
            var error = Assert.Throws<VfsException>(() => vfs.Open("/f", OpenMode.Write, User(300, 999)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Owner_class_is_exclusive_even_when_other_class_is_more_permissive()
        {
            var vfs = FileOwnedBy(100, 500, 0b000_100_100);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/f", OpenMode.Read, User(100, 999)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }
    }
}
