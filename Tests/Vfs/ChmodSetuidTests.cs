using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Vfs.Tests
{
    /// <summary>
    /// Pins the setuid rules at the VFS door: only root may ADD the bit (a non-root owner gets
    /// EPERM), a non-root owner may still clear it, and a successful non-root write drops the
    /// bit (POSIX drop-on-write) while a root write keeps it.
    /// </summary>
    [TestFixture]
    public sealed class ChmodSetuidTests
    {
        private const int RwxRxRx = 0b111_101_101;
        private static readonly Credentials Root = new Credentials(0);
        private static readonly Credentials Owner = new Credentials(1000);

        private static PermissionMode Setuid(int lowBits) => new PermissionMode(PermissionMode.SetUidBit | lowBits);

        private static VirtualFileSystem OwnedFile(string path, PermissionMode mode)
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile(path, mode, Root);
            vfs.Chown(path, Owner.Uid, 1000, Root);
            return vfs;
        }

        [Test]
        public void Non_root_cannot_set_the_setuid_bit()
        {
            var vfs = OwnedFile("/f", new PermissionMode(RwxRxRx));

            Assert.That(
                () => vfs.Chmod("/f", Setuid(RwxRxRx), Owner),
                Throws.InstanceOf<VfsException>().With.Property("Error").EqualTo(VfsError.EPERM));
            Assert.That(vfs.Stat("/f", Root).Mode.SetUid, Is.False);
        }

        [Test]
        public void Root_sets_the_setuid_bit()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/f", new PermissionMode(RwxRxRx), Root);

            vfs.Chmod("/f", Setuid(RwxRxRx), Root);

            Assert.That(vfs.Stat("/f", Root).Mode.SetUid, Is.True);
        }

        [Test]
        public void Non_root_owner_can_clear_the_setuid_bit()
        {
            var vfs = OwnedFile("/f", Setuid(RwxRxRx));

            vfs.Chmod("/f", new PermissionMode(RwxRxRx), Owner);

            Assert.That(vfs.Stat("/f", Root).Mode.SetUid, Is.False);
        }

        [Test]
        public void An_unprivileged_write_drops_the_setuid_bit_but_a_root_write_keeps_it()
        {
            var byOwner = OwnedFile("/owned", Setuid(RwxRxRx));
            byOwner.OpenForWrite("/owned", WriteBehavior.Truncate, new PermissionMode(0b110_100_100), Owner).CloseWrite();
            Assert.That(byOwner.Stat("/owned", Root).Mode.SetUid, Is.False);

            var byRoot = new VirtualFileSystem();
            byRoot.CreateFile("/tool", Setuid(RwxRxRx), Root);
            byRoot.OpenForWrite("/tool", WriteBehavior.Truncate, new PermissionMode(0b110_100_100), Root).CloseWrite();
            Assert.That(byRoot.Stat("/tool", Root).Mode.SetUid, Is.True);
        }

        [Test]
        public void An_unprivileged_append_also_drops_the_setuid_bit()
        {
            var vfs = OwnedFile("/owned", Setuid(RwxRxRx));

            vfs.OpenForWrite("/owned", WriteBehavior.Append, new PermissionMode(0b110_100_100), Owner).CloseWrite();

            Assert.That(vfs.Stat("/owned", Root).Mode.SetUid, Is.False);
        }

        [Test]
        public void A_non_root_open_for_write_drops_setuid_but_read_and_root_write_keep_it()
        {
            var written = OwnedFile("/w", Setuid(RwxRxRx));
            written.Open("/w", OpenMode.Write, Owner).CloseWrite();
            Assert.That(written.Stat("/w", Root).Mode.SetUid, Is.False);

            var readWritten = OwnedFile("/rw", Setuid(RwxRxRx));
            readWritten.Open("/rw", OpenMode.ReadWrite, Owner).CloseWrite();
            Assert.That(readWritten.Stat("/rw", Root).Mode.SetUid, Is.False);

            var read = OwnedFile("/r", Setuid(RwxRxRx));
            read.Open("/r", OpenMode.Read, Owner).CloseRead();
            Assert.That(read.Stat("/r", Root).Mode.SetUid, Is.True);

            var byRoot = new VirtualFileSystem();
            byRoot.CreateFile("/tool", Setuid(RwxRxRx), Root);
            byRoot.Open("/tool", OpenMode.Write, Root).CloseWrite();
            Assert.That(byRoot.Stat("/tool", Root).Mode.SetUid, Is.True);
        }

        [Test]
        public void A_non_root_creator_cannot_introduce_setuid_but_root_can()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/shared", new PermissionMode(0b111_111_111), Root);

            vfs.CreateFile("/shared/mine", Setuid(RwxRxRx), Owner);
            Assert.That(vfs.Stat("/shared/mine", Root).Mode.SetUid, Is.False);

            vfs.CreateFile("/tool", Setuid(RwxRxRx), Root);
            Assert.That(vfs.Stat("/tool", Root).Mode.SetUid, Is.True);
        }

        [Test]
        public void A_non_root_copy_strips_setuid_while_a_root_copy_keeps_it()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/src", Setuid(RwxRxRx), Root);
            vfs.CreateDirectory("/shared", new PermissionMode(0b111_111_111), Root);

            vfs.Copy("/src", "/shared/mine", Owner);
            var mine = vfs.Stat("/shared/mine", Root);
            Assert.That(mine.Mode.SetUid, Is.False);
            Assert.That(mine.OwnerUid, Is.EqualTo(Owner.Uid));

            vfs.Copy("/src", "/roots", Root);
            Assert.That(vfs.Stat("/roots", Root).Mode.SetUid, Is.True);
        }

        [Test]
        public void A_non_root_owner_may_rechmod_a_setuid_file_keeping_the_bit()
        {
            var vfs = OwnedFile("/f", Setuid(RwxRxRx));

            vfs.Chmod("/f", Setuid(0b111_101_111), Owner);

            var mode = vfs.Stat("/f", Root).Mode;
            Assert.That(mode.SetUid, Is.True);
            Assert.That(mode.OtherRwx, Is.EqualTo(0b111));
        }
    }
}
