using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Siegebox.Vfs.Tests
{
    /// <summary>
    /// Pins the Import boundary hardening: a malicious or corrupt snapshot cannot smuggle a
    /// duplicate child, a control-character name, an over-deep tree, or a reference cycle past
    /// <see cref="VirtualFileSystem.Import"/>. Also pins deterministic ordinal child ordering
    /// in the exported snapshot so a save round-trips byte-stably regardless of dictionary
    /// insertion history.
    /// </summary>
    [TestFixture]
    public sealed class VfsImportHardeningTests
    {
        private const int MaxImportDepth = 64;

        private static readonly Credentials Root = new Credentials(0);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        private static VfsNodeSnapshot Directory(string name, params VfsNodeSnapshot[] children)
            => new VfsNodeSnapshot
            {
                Type = NodeType.Directory,
                Name = name,
                Children = new List<VfsNodeSnapshot>(children)
            };

        private static VfsNodeSnapshot File(string name)
            => new VfsNodeSnapshot { Type = NodeType.File, Name = name };

        [Test]
        public void Import_rejects_a_directory_with_duplicate_child_names()
        {
            var root = Directory("", File("dup"), File("dup"));

            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void Import_rejects_a_null_child_snapshot()
        {
            var root = Directory("");
            root.Children.Add(null);

            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void Import_rejects_a_child_name_with_a_control_character()
        {
            var root = Directory("", File("badname"));

            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void Import_rejects_a_tree_deeper_than_the_import_depth_cap()
        {
            VfsNodeSnapshot current = Directory("leaf");
            for (var level = 0; level < 65; level++)
            {
                current = Directory("d" + level, current);
            }

            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(current));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void Import_accepts_a_tree_at_exactly_the_import_depth_cap()
        {
            VfsNodeSnapshot current = Directory("leaf");
            for (var level = 0; level < MaxImportDepth; level++)
            {
                current = Directory("d" + level, current);
            }

            Assert.That(() => VirtualFileSystem.Import(current), Throws.Nothing);
        }

        [Test]
        public void Import_rejects_a_reference_cycle_of_the_same_snapshot_instance()
        {
            var looped = Directory("a");
            looped.Children.Add(looped);
            var root = Directory("", looped);

            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void Import_rejects_the_same_snapshot_instance_shared_by_two_parents()
        {
            var shared = File("shared");
            var root = Directory("", Directory("left", shared), Directory("right", shared));

            var error = Assert.Throws<VfsException>(() => VirtualFileSystem.Import(root));
            Assert.That(error.Error, Is.EqualTo(VfsError.EINVAL));
        }

        [Test]
        public void Export_orders_children_ordinally_regardless_of_insertion_history()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/d", Mode(0b111_101_101), Root);
            vfs.CreateFile("/d/c", Mode(0b110_100_100), Root);
            vfs.CreateFile("/d/a", Mode(0b110_100_100), Root);
            vfs.CreateFile("/d/b", Mode(0b110_100_100), Root);
            vfs.Delete("/d/a", Root);
            vfs.CreateFile("/d/a", Mode(0b110_100_100), Root);

            var directory = FindChild(vfs.Export(), "d");
            var names = directory.Children!.Select(child => child.Name).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "a", "b", "c" }));
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
    }
}
