using System.Text;
using System.Text.Json;
using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    [TestFixture]
    public sealed class VirtualFileSystemOperationsTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static Credentials User(int uid, params int[] groupIds) => new Credentials(uid, groupIds);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        [Test]
        public void Create_write_and_read_a_file()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/note", Mode(0b110_100_100), Root);
            Write(vfs, "/note", "content");
            Assert.That(Read(vfs, "/note"), Is.EqualTo("content"));
        }

        [Test]
        public void List_returns_child_names_sorted()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/d", Mode(0b111_101_101), Root);
            vfs.CreateFile("/d/charlie", Mode(0b110_100_100), Root);
            vfs.CreateFile("/d/alpha", Mode(0b110_100_100), Root);
            vfs.CreateFile("/d/bravo", Mode(0b110_100_100), Root);
            Assert.That(vfs.List("/d", Root), Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
        }

        [Test]
        public void Delete_removes_a_file()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/gone", Mode(0b110_100_100), Root);
            vfs.Delete("/gone", Root);
            Assert.That(() => vfs.Open("/gone", OpenMode.Read, Root), Throws.TypeOf<VfsException>());
        }

        [Test]
        public void Delete_on_a_non_empty_directory_yields_ENOTEMPTY()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/d", Mode(0b111_101_101), Root);
            vfs.CreateFile("/d/child", Mode(0b110_100_100), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Delete("/d", Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ENOTEMPTY));
        }

        [Test]
        public void Move_relocates_a_node_and_rejects_an_existing_destination()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/src", Mode(0b110_100_100), Root);
            Write(vfs, "/src", "moved");
            vfs.Move("/src", "/dst", Root);
            Assert.That(Read(vfs, "/dst"), Is.EqualTo("moved"));
            Assert.That(() => vfs.Open("/src", OpenMode.Read, Root), Throws.TypeOf<VfsException>());

            vfs.CreateFile("/occupied", Mode(0b110_100_100), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Move("/dst", "/occupied", Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EEXIST));
        }

        [Test]
        public void Move_into_own_subtree_is_rejected_and_leaves_the_tree_intact()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/a", Mode(0b111_101_101), Root);
            vfs.CreateDirectory("/a/b", Mode(0b111_101_101), Root);

            var intoChild = Assert.Throws<VfsException>(() => vfs.Move("/a", "/a/b", Root));
            Assert.That(intoChild.Error, Is.EqualTo(VfsError.EINVAL));
            var intoDescendant = Assert.Throws<VfsException>(() => vfs.Move("/a", "/a/b/c", Root));
            Assert.That(intoDescendant.Error, Is.EqualTo(VfsError.EINVAL));
            var ontoSelf = Assert.Throws<VfsException>(() => vfs.Move("/a", "/a", Root));
            Assert.That(ontoSelf.Error, Is.EqualTo(VfsError.EINVAL));

            Assert.That(vfs.List("/", Root), Does.Contain("a"));
            Assert.That(vfs.List("/a", Root), Does.Contain("b"));
            Assert.That(() => vfs.Export(), Throws.Nothing);
        }

        [Test]
        public void Operations_on_the_root_path_yield_EINVAL()
        {
            var vfs = new VirtualFileSystem();
            var error = Assert.Throws<VfsException>(() => vfs.CreateFile("/", Mode(0b110_100_100), Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void CreateFile_over_an_existing_path_yields_EEXIST()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_100_100), Root);
            var error = Assert.Throws<VfsException>(() => vfs.CreateFile("/f", Mode(0b110_100_100), Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EEXIST));
        }

        [Test]
        public void Copy_duplicates_file_content_and_rejects_a_directory_source()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/src", Mode(0b110_100_100), Root);
            Write(vfs, "/src", "duplicated");
            vfs.Copy("/src", "/dst", Root);
            Assert.That(Read(vfs, "/dst"), Is.EqualTo("duplicated"));
            Assert.That(Read(vfs, "/src"), Is.EqualTo("duplicated"));

            vfs.CreateDirectory("/dir", Mode(0b111_101_101), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Copy("/dir", "/dircopy", Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EISDIR));
        }

        [Test]
        public void Open_on_a_directory_yields_EISDIR()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/d", Mode(0b111_101_101), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/d", OpenMode.Read, Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EISDIR));
        }

        [Test]
        public void Chmod_is_allowed_for_the_owner_and_denied_for_others()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_100_100), Root);
            vfs.Chown("/f", 100, 500, Root);
            vfs.Chmod("/f", Mode(0b111_000_000), User(100, 500));
            Assert.That(vfs.Stat("/f", Root).Mode, Is.EqualTo(Mode(0b111_000_000)));
            var error = Assert.Throws<VfsException>(() => vfs.Chmod("/f", Mode(0), User(200)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EPERM));
        }

        [Test]
        public void Chown_is_restricted_to_root()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_100_100), Root);
            vfs.Chown("/f", 100, 500, Root);
            var error = Assert.Throws<VfsException>(() => vfs.Chown("/f", 100, 500, User(100, 500)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EPERM));
        }

        [Test]
        public void Symlink_operation_creates_a_working_link()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/target", Mode(0b110_110_110), Root);
            Write(vfs, "/target", "linked");
            vfs.CreateSymlink("/link", "/target", Root);
            Assert.That(Read(vfs, "/link"), Is.EqualTo("linked"));
        }

        [Test]
        public void Probe_foreign_uid_is_denied()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_000_000), Root);
            vfs.Chown("/f", 100, 500, Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/f", OpenMode.Read, User(200)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Probe_traverse_denied_without_execute()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/private", Mode(0b111_000_000), Root);
            vfs.CreateFile("/private/data", Mode(0b110_110_110), Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/private/data", OpenMode.Read, User(100)));
            Assert.That(error.Error, Is.EqualTo(VfsError.EACCES));
        }

        [Test]
        public void Probe_symlink_loop_yields_ELOOP()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateSymlink("/a", "/b", Root);
            vfs.CreateSymlink("/b", "/a", Root);
            var error = Assert.Throws<VfsException>(() => vfs.Open("/a", OpenMode.Read, Root));
            Assert.That(error.Error, Is.EqualTo(VfsError.ELOOP));
        }

        [Test]
        public void Probe_pipe_reports_eof_after_writer_close()
        {
            var pipe = new PipeStream();
            pipe.Write(new byte[] { 1 }, 0, 1);
            pipe.CloseWrite();
            pipe.Read(new byte[1], 0, 1);
            Assert.That(pipe.Read(new byte[1], 0, 1).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void Probe_stream_capability_outlives_chmod()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_000_000), Root);
            vfs.Chown("/f", 100, 500, Root);
            var owner = User(100, 500);
            Write(vfs, "/f", "kept", owner);
            var reader = vfs.Open("/f", OpenMode.Read, owner);
            vfs.Chmod("/f", Mode(0), owner);
            var buffer = new byte[16];
            var result = reader.Read(buffer, 0, buffer.Length);
            Assert.That(Encoding.UTF8.GetString(buffer, 0, result.Count), Is.EqualTo("kept"));
        }

        [Test]
        public void Probe_dev_null_is_reachable_through_open()
        {
            var vfs = new VirtualFileSystem();
            var sink = vfs.Open("/dev/null", OpenMode.Write, User(100));
            var write = sink.Write(new byte[] { 1, 2, 3 }, 0, 3);
            Assert.That(write.Status, Is.EqualTo(StreamStatus.Ok));
            Assert.That(write.Count, Is.EqualTo(3));

            var source = vfs.Open("/dev/null", OpenMode.Read, User(100));
            Assert.That(source.Read(new byte[3], 0, 3).Status, Is.EqualTo(StreamStatus.Eof));
        }

        [Test]
        public void Tree_round_trips_through_snapshot_serialization()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/home", Mode(0b111_101_101), Root);
            vfs.CreateFile("/home/note.txt", Mode(0b110_100_100), Root);
            Write(vfs, "/home/note.txt", "persist");
            vfs.CreateSymlink("/home/shortcut", "note.txt", Root);

            var json = JsonSerializer.Serialize(vfs.Export());
            var snapshot = JsonSerializer.Deserialize<VfsNodeSnapshot>(json);
            var restored = VirtualFileSystem.Import(snapshot);

            Assert.That(Read(restored, "/home/note.txt"), Is.EqualTo("persist"));
            Assert.That(Read(restored, "/home/shortcut"), Is.EqualTo("persist"));
            Assert.That(restored.List("/home", Root), Does.Contain("note.txt"));

            var sink = restored.Open("/dev/null", OpenMode.Write, User(100));
            Assert.That(sink.Write(new byte[] { 9 }, 0, 1).Status, Is.EqualTo(StreamStatus.Ok));
        }

        [Test]
        public void Snapshot_round_trip_preserves_mode_owner_and_group()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", Mode(0b110_100_000), Root);
            vfs.Chown("/f", 100, 500, Root);
            var restored = RoundTrip(vfs);

            var info = restored.Stat("/f", Root);
            Assert.That(info.Mode, Is.EqualTo(Mode(0b110_100_000)));
            Assert.That(info.OwnerUid, Is.EqualTo(100));
            Assert.That(info.GroupGid, Is.EqualTo(500));
        }

        [Test]
        public void Device_nodes_round_trip_with_their_device_kind()
        {
            var vfs = new VirtualFileSystem();
            var restored = RoundTrip(vfs);

            var devNull = FindChild(FindChild(restored.Export(), "dev"), "null");
            Assert.That(devNull.Type, Is.EqualTo(NodeType.Device));
            Assert.That(devNull.DeviceKind, Is.EqualTo(DeviceKind.Null));
        }

        [Test]
        public void Import_rejects_a_non_directory_root_snapshot()
        {
            var snapshot = new VfsNodeSnapshot { Type = NodeType.File, Name = "root" };
            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(snapshot));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void A_user_can_create_within_a_world_writable_directory()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/pub", Mode(0b111_111_111), Root);
            vfs.CreateFile("/pub/mine", Mode(0b110_100_100), User(100));
            Assert.That(vfs.Stat("/pub/mine", Root).OwnerUid, Is.EqualTo(100));
        }

        private static VirtualFileSystem RoundTrip(VirtualFileSystem vfs)
        {
            var json = JsonSerializer.Serialize(vfs.Export());
            return VirtualFileSystem.Import(JsonSerializer.Deserialize<VfsNodeSnapshot>(json));
        }

        private static VfsNodeSnapshot FindChild(VfsNodeSnapshot parent, string name)
        {
            foreach (var child in parent.Children!)
            {
                if (child.Name == name)
                {
                    return child;
                }
            }

            throw new AssertionException($"Snapshot child '{name}' was not found.");
        }

        private static void Write(VirtualFileSystem vfs, string path, string text) => Write(vfs, path, text, Root);

        private static void Write(VirtualFileSystem vfs, string path, string text, Credentials credentials)
        {
            var stream = vfs.Open(path, OpenMode.Write, credentials);
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
