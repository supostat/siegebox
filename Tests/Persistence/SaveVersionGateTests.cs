using System.Collections.Generic;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Persistence.Tests
{
    /// <summary>
    /// Pins the load-time gate ordering: the version is checked BEFORE the tree is trusted, an
    /// unsupported or zero version is rejected with a <see cref="SaveFormatException"/>, and a
    /// missing or non-directory root is rejected the same way. A save that is both wrong-version
    /// AND structurally invalid must surface the version failure, never a VFS import failure.
    /// </summary>
    [TestFixture]
    public sealed class SaveVersionGateTests
    {
        private static VfsNodeSnapshot ValidRoot() => new VirtualFileSystem().Export();

        private static VfsNodeSnapshot File(string name)
            => new VfsNodeSnapshot { Type = NodeType.File, Name = name };

        [Test]
        public void A_future_version_is_rejected()
        {
            var save = new SaveGame { Version = SaveVersion.Current + 1, Root = ValidRoot() };

            Assert.Throws<SaveFormatException>(() => SaveSerializer.Load(save));
        }

        [Test]
        public void A_zero_version_is_rejected()
        {
            var save = new SaveGame { Version = 0, Root = ValidRoot() };

            Assert.Throws<SaveFormatException>(() => SaveSerializer.Load(save));
        }

        [Test]
        public void The_current_version_with_a_valid_root_loads()
        {
            var save = new SaveGame { Version = SaveVersion.Current, Root = ValidRoot() };

            Assert.That(() => SaveSerializer.Load(save), Throws.Nothing);
        }

        [Test]
        public void A_null_root_is_rejected()
        {
            var save = new SaveGame { Version = SaveVersion.Current, Root = null };

            Assert.Throws<SaveFormatException>(() => SaveSerializer.Load(save));
        }

        [Test]
        public void A_non_directory_root_is_rejected()
        {
            var save = new SaveGame { Version = SaveVersion.Current, Root = File("root") };

            Assert.Throws<SaveFormatException>(() => SaveSerializer.Load(save));
        }

        [Test]
        public void A_bad_version_beats_an_invalid_tree_and_yields_a_format_error_not_a_vfs_error()
        {
            var invalidRoot = new VfsNodeSnapshot
            {
                Type = NodeType.Directory,
                Name = string.Empty,
                Children = new List<VfsNodeSnapshot> { File("dup"), File("dup") }
            };
            var save = new SaveGame { Version = SaveVersion.Current + 1, Root = invalidRoot };

            Assert.Throws<SaveFormatException>(() => SaveSerializer.Load(save));
        }
    }
}
