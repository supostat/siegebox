using System.Text;
using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class OpenForWriteTests
    {
        private static readonly Credentials Root = new Credentials(0);
        private static readonly PermissionMode Mode644 = new PermissionMode(0b110_100_100);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static void WriteAll(IByteStream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            var result = stream.Write(bytes, 0, bytes.Length);
            Assert.That(result.Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(result.Count, Is.EqualTo(bytes.Length));
        }

        private static string ReadAll(VirtualFileSystem vfs, string path)
        {
            var stream = vfs.Open(path, OpenMode.Read, Root);
            var builder = new StringBuilder();
            var chunk = new byte[64];
            while (true)
            {
                var result = stream.Read(chunk, 0, chunk.Length);
                if (result.Status != StreamStatus.Ok)
                {
                    return builder.ToString();
                }

                builder.Append(Encoding.UTF8.GetString(chunk, 0, result.Count));
            }
        }

        [Test]
        public void Truncate_leaves_only_the_new_bytes()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode644, Root);
            WriteAll(vfs.OpenForWrite("/f", WriteBehavior.Truncate, Mode644, Root), "stale-content");

            WriteAll(vfs.OpenForWrite("/f", WriteBehavior.Truncate, Mode644, Root), "new");

            Assert.That(ReadAll(vfs, "/f"), Is.EqualTo("new"));
        }

        [Test]
        public void Append_writes_after_the_existing_content()
        {
            var vfs = new VirtualFileSystem();
            WriteAll(vfs.OpenForWrite("/f", WriteBehavior.Truncate, Mode644, Root), "one");

            WriteAll(vfs.OpenForWrite("/f", WriteBehavior.Append, Mode644, Root), "two");

            Assert.That(ReadAll(vfs, "/f"), Is.EqualTo("onetwo"));
        }

        [Test]
        public void Create_uses_the_given_mode_and_creator_uid()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/home", new PermissionMode(0b111_111_111), Root);
            var user = User(1000);

            WriteAll(vfs.OpenForWrite("/home/f", WriteBehavior.Truncate, Mode644, user), "hi");

            var info = vfs.Stat("/home/f", Root);
            Assert.That(info.OwnerUid, Is.EqualTo(1000));
            Assert.That(info.Mode, Is.EqualTo(Mode644));
            Assert.That(ReadAll(vfs, "/home/f"), Is.EqualTo("hi"));
        }

        [Test]
        public void Append_creates_a_missing_file()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/home", new PermissionMode(0b111_111_111), Root);

            WriteAll(vfs.OpenForWrite("/home/made", WriteBehavior.Append, Mode644, User(1000)), "made");

            Assert.That(ReadAll(vfs, "/home/made"), Is.EqualTo("made"));
            Assert.That(vfs.Stat("/home/made", Root).OwnerUid, Is.EqualTo(1000));
            Assert.That(vfs.Stat("/home/made", Root).Mode, Is.EqualTo(Mode644));
        }

        [Test]
        public void Two_appenders_interleave_at_the_live_end()
        {
            var vfs = new VirtualFileSystem();
            WriteAll(vfs.OpenForWrite("/f", WriteBehavior.Truncate, Mode644, Root), "a");
            var first = vfs.OpenForWrite("/f", WriteBehavior.Append, Mode644, Root);
            var second = vfs.OpenForWrite("/f", WriteBehavior.Append, Mode644, Root);

            WriteAll(first, "b");
            WriteAll(second, "c");
            WriteAll(first, "d");

            Assert.That(ReadAll(vfs, "/f"), Is.EqualTo("abcd"));
        }

        [Test]
        public void Device_write_passes_through()
        {
            var vfs = new VirtualFileSystem();

            var stream = vfs.OpenForWrite("/dev/null", WriteBehavior.Truncate, Mode644, Root);

            WriteAll(stream, "discarded");
        }

        [Test]
        public void Missing_parent_yields_ENOENT()
        {
            var vfs = new VirtualFileSystem();

            var error = Assert.Throws<VfsException>(
                () => vfs.OpenForWrite("/nowhere/f", WriteBehavior.Truncate, Mode644, Root));

            Assert.That(error.Error, Is.EqualTo(VfsError.ENOENT));
        }

        [Test]
        public void Create_in_an_unwritable_parent_yields_EACCES()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/locked", new PermissionMode(0b111_101_101), Root);

            var error = Assert.Throws<VfsException>(
                () => vfs.OpenForWrite("/locked/f", WriteBehavior.Truncate, Mode644, User(1000)));

            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Writing_an_unwritable_file_yields_EACCES()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/readonly", new PermissionMode(0b100_100_100), Root);

            var error = Assert.Throws<VfsException>(
                () => vfs.OpenForWrite("/readonly", WriteBehavior.Append, Mode644, User(1000)));

            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Directory_target_yields_EISDIR()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/d", new PermissionMode(0b111_101_101), Root);

            var error = Assert.Throws<VfsException>(
                () => vfs.OpenForWrite("/d", WriteBehavior.Truncate, Mode644, Root));

            Assert.That(error.Error, Is.EqualTo(VfsError.EISDIR));
        }

        [Test]
        public void Create_through_a_dangling_final_symlink_creates_the_target()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateSymlink("/link", "/target", Root);

            WriteAll(vfs.OpenForWrite("/link", WriteBehavior.Truncate, Mode644, Root), "via-link");

            Assert.That(vfs.Stat("/link", Root).Type, Is.EqualTo(NodeType.Symlink));
            Assert.That(vfs.Stat("/target", Root).Type, Is.EqualTo(NodeType.File));
            Assert.That(ReadAll(vfs, "/target"), Is.EqualTo("via-link"));
            Assert.That(ReadAll(vfs, "/link"), Is.EqualTo("via-link"));
        }

        [Test]
        public void Symlink_loop_yields_ELOOP()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateSymlink("/a", "/b", Root);
            vfs.CreateSymlink("/b", "/a", Root);

            var error = Assert.Throws<VfsException>(
                () => vfs.OpenForWrite("/a", WriteBehavior.Truncate, Mode644, Root));

            Assert.That(error.Error, Is.EqualTo(VfsError.ELOOP));
        }
    }
}
