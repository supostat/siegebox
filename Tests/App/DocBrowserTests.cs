using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Siegebox.Documentation;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.App.Tests
{
    [TestFixture]
    public sealed class DocBrowserTests
    {
        private static readonly Credentials Root = new Credentials(0);
        private static readonly Credentials Player = new Credentials(1000);

        [Test]
        public void Categories_group_live_commands_and_default_unknown_to_other()
        {
            var browser = BrowserFor(Root, "cat", "ls", "modcmd");

            var categories = browser.Categories;

            Assert.That(CategoryNamed(categories, "text").Entries[0].CommandName, Is.EqualTo("cat"));
            Assert.That(CategoryNamed(categories, "file system").Entries[0].CommandName, Is.EqualTo("ls"));
            var other = CategoryNamed(categories, "other");
            Assert.That(other.Entries[0].CommandName, Is.EqualTo("modcmd"));
            Assert.That(other.Entries[0].Description, Is.Empty);
        }

        [Test]
        public void Categories_sort_entries_ordinal_within_a_shared_category_and_categories_overall()
        {
            var browser = BrowserFor(Root, "echo", "ls", "cat");

            var categories = browser.Categories;

            var text = CategoryNamed(categories, "text");
            Assert.That(text.Entries[0].CommandName, Is.EqualTo("cat"));
            Assert.That(text.Entries[1].CommandName, Is.EqualTo("echo"));

            var categoryNames = new List<string>();
            foreach (var category in categories)
            {
                categoryNames.Add(category.Name);
            }

            Assert.That(categoryNames, Is.Ordered.Using((IComparer<string>)StringComparer.Ordinal));
        }

        [Test]
        public void Register_rejects_a_duplicate_name()
        {
            var manual = new Manual();
            manual.Register(new ManualPage("dup", "text", "usage: dup", "A duplicate."));

            Assert.Throws<ArgumentException>(
                () => manual.Register(new ManualPage("dup", "text", "usage: dup", "A duplicate.")));
        }

        [Test]
        public void ManualPage_rejects_a_blank_required_field()
            => Assert.Throws<ArgumentException>(() => new ManualPage(" ", "text", "usage: x", "A page."));

        [Test]
        public void ReadPage_returns_the_seeded_body()
        {
            var browser = BrowserFor(Root, "cat");

            Assert.That(browser.ReadPage("cat"), Does.Contain("concatenate files"));
        }

        [Test]
        public void ReadPage_renders_a_missing_page_in_cat_style()
        {
            var browser = BrowserFor(Root, "cat");

            Assert.That(browser.ReadPage("nope"), Is.EqualTo("man: /usr/share/man/nope: No such file or directory"));
        }

        [Test]
        public void ReadPage_reports_permission_denied_for_a_non_owner()
        {
            var vfs = SeededVfs();
            WriteRootOnlyPage(vfs, "secret");
            var browser = new DocBrowser(() => new List<string> { "cat" }, SeededManual(), vfs, Player, BoxSeed.ManifestPath);

            Assert.That(browser.ReadPage("secret"), Is.EqualTo("man: /usr/share/man/secret: Permission denied"));
        }

        [Test]
        public void ReadHints_returns_the_manifest_hints_when_seeded()
        {
            var vfs = SeededVfs();
            BoxSeed.Seed(vfs);
            var browser = new DocBrowser(() => new List<string>(), SeededManual(), vfs, Player, BoxSeed.ManifestPath);

            Assert.That(browser.ReadHints(), Does.Contain("Try `man ls` to read a manual page."));
        }

        [Test]
        public void ReadHints_is_empty_when_the_manifest_is_absent()
        {
            var browser = BrowserFor(Root, "cat");

            Assert.That(browser.ReadHints(), Is.Empty);
        }

        [Test]
        public void Parse_reads_a_valid_manifest()
        {
            var manifest = BoxManifest.Parse(@"{""target"":""box"",""hints"":[""a"",""b""]}");

            Assert.That(manifest.Target, Is.EqualTo("box"));
            Assert.That(manifest.Hints, Is.EqualTo(new[] { "a", "b" }));
        }

        [Test]
        public void Parse_defaults_optional_fields_when_absent()
        {
            var manifest = BoxManifest.Parse("{}");

            Assert.That(manifest.Target, Is.Empty);
            Assert.That(manifest.Hints, Is.Empty);
        }

        [Test]
        public void Parse_rejects_malformed_json()
            => Assert.Throws<BoxManifestException>(() => BoxManifest.Parse("{"));

        [Test]
        public void Parse_rejects_a_non_table_root()
            => Assert.Throws<BoxManifestException>(() => BoxManifest.Parse("42"));

        [Test]
        public void Parse_rejects_a_null_hints_field()
            => Assert.Throws<BoxManifestException>(() => BoxManifest.Parse(@"{""hints"":null}"));

        [Test]
        public void Parse_rejects_a_null_hints_element()
            => Assert.Throws<BoxManifestException>(() => BoxManifest.Parse(@"{""hints"":[""a"",null]}"));

        [Test]
        public void Parse_rejects_a_null_target()
            => Assert.Throws<BoxManifestException>(() => BoxManifest.Parse(@"{""target"":null}"));

        private static DocBrowser BrowserFor(Credentials credentials, params string[] commandNames)
        {
            var names = new List<string>(commandNames);
            return new DocBrowser(() => names, SeededManual(), SeededVfs(), credentials, BoxSeed.ManifestPath);
        }

        private static Manual SeededManual()
        {
            var manual = new Manual();
            ManualSeed.RegisterInto(manual);
            return manual;
        }

        private static VirtualFileSystem SeededVfs()
        {
            var vfs = new VirtualFileSystem();
            UserSeed.Seed(vfs);
            BinSeed.Seed(vfs);
            ManualSeed.SeedPages(vfs);
            return vfs;
        }

        private static void WriteRootOnlyPage(VirtualFileSystem vfs, string name)
        {
            var stream = vfs.OpenForWrite(
                "/usr/share/man/" + name, WriteBehavior.Truncate, new PermissionMode(0b110_000_000), Root);
            var bytes = Encoding.UTF8.GetBytes("classified\n");
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
        }

        private static DocCategory CategoryNamed(IReadOnlyList<DocCategory> categories, string name)
        {
            foreach (var category in categories)
            {
                if (category.Name == name)
                {
                    return category;
                }
            }

            throw new AssertionException($"No category named '{name}'.");
        }
    }
}
