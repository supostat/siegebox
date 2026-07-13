using System;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Vfs.Tests
{
    /// <summary>
    /// Pins the 12-bit permission model: the setuid bit round-trips through the mode, renders
    /// as s/S in the owner-execute slot, bounds the valid range at 2559, and survives an
    /// Export/Import cycle while an old 0..511 snapshot still imports (Phase-8 compatibility).
    /// </summary>
    [TestFixture]
    public sealed class PermissionModeSetuidTests
    {
        private const int RwxRxRx = 0b111_101_101;
        private static readonly Credentials Root = new Credentials(0);

        [Test]
        public void PermissionMode_roundtrips_setuid_bit()
        {
            var setuid = new PermissionMode(PermissionMode.SetUidBit | RwxRxRx);

            Assert.That(setuid.SetUid, Is.True);
            Assert.That(setuid.WithSetUid(false).SetUid, Is.False);
            Assert.That(setuid.WithSetUid(false).Bits, Is.EqualTo(RwxRxRx));

            var raised = new PermissionMode(RwxRxRx).WithSetUid(true);
            Assert.That(raised.SetUid, Is.True);
            Assert.That(raised.Bits, Is.EqualTo(PermissionMode.SetUidBit | RwxRxRx));
        }

        [Test]
        public void PermissionMode_renders_s_and_capital_s_in_tostring()
        {
            var withExecute = new PermissionMode(PermissionMode.SetUidBit | 0b111_101_101);
            Assert.That(withExecute.ToString(), Is.EqualTo("rwsr-xr-x"));

            var withoutExecute = new PermissionMode(PermissionMode.SetUidBit | 0b110_101_101);
            Assert.That(withoutExecute.ToString(), Is.EqualTo("rwSr-xr-x"));
        }

        [Test]
        public void PermissionMode_renders_a_normal_owner_execute_slot_without_setuid()
        {
            Assert.That(new PermissionMode(0b111_101_101).ToString(), Is.EqualTo("rwxr-xr-x"));
            Assert.That(new PermissionMode(0b110_100_100).ToString(), Is.EqualTo("rw-r--r--"));
        }

        [Test]
        public void PermissionMode_rejects_the_gapped_and_out_of_range_bits()
        {
            Assert.That(() => new PermissionMode(0), Throws.Nothing);
            Assert.That(() => new PermissionMode(511), Throws.Nothing);
            Assert.That(() => new PermissionMode(2559), Throws.Nothing);
            Assert.Throws<ArgumentOutOfRangeException>(() => new PermissionMode(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PermissionMode(512));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PermissionMode(1024));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PermissionMode(2560));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PermissionMode(4096));
        }

        [Test]
        public void Export_and_import_preserve_the_setuid_bit()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateFile("/tool", new PermissionMode(PermissionMode.SetUidBit | RwxRxRx), Root);

            var reloaded = VirtualFileSystem.Import(vfs.Export());

            Assert.That(reloaded.Stat("/tool", Root).Mode.SetUid, Is.True);
        }

        [Test]
        public void An_old_snapshot_without_setuid_still_imports()
        {
            var snapshot = new VfsNodeSnapshot
            {
                Type = NodeType.Directory,
                Name = string.Empty,
                ModeBits = 493
            };

            var imported = VirtualFileSystem.Import(snapshot);

            var mode = imported.Stat("/", Root).Mode;
            Assert.That(mode.Bits, Is.EqualTo(493));
            Assert.That(mode.SetUid, Is.False);
        }

        [Test]
        public void A_snapshot_with_out_of_range_mode_bits_is_rejected_as_EINVAL()
        {
            var snapshot = new VfsNodeSnapshot
            {
                Type = NodeType.Directory,
                Name = string.Empty,
                ModeBits = 4096
            };

            Assert.That(
                () => VirtualFileSystem.Import(snapshot),
                Throws.InstanceOf<VfsException>().With.Property("Error").EqualTo(VfsError.EINVAL));
        }
    }
}
