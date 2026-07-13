using System;
using NUnit.Framework;

namespace Siegebox.Security.Tests
{
    /// <summary>
    /// Pins ordered shadow serialization: SetHash updates an existing entry in place, appends
    /// a new one at the end, and Render round-trips the file preserving both order and the
    /// other untouched entries.
    /// </summary>
    [TestFixture]
    public sealed class ShadowTableSerializationTests
    {
        [Test]
        public void SetHash_updates_an_existing_entry_in_place()
        {
            var shadow = ShadowTable.Parse("root:aa$bb\nplayer:cc$dd\n");

            shadow.SetHash("player", "ee$ff");

            Assert.That(shadow.Render(), Is.EqualTo("root:aa$bb\nplayer:ee$ff\n"));
        }

        [Test]
        public void SetHash_appends_an_unknown_entry_at_the_end()
        {
            var shadow = ShadowTable.Parse("root:aa$bb\n");

            shadow.SetHash("player", "cc$dd");

            Assert.That(shadow.Render(), Is.EqualTo("root:aa$bb\nplayer:cc$dd\n"));
        }

        [Test]
        public void Parse_then_render_round_trips_and_hashes_resolve()
        {
            const string text = "root:aa$bb\nplayer:cc$dd\n";

            var shadow = ShadowTable.Parse(text);

            Assert.That(shadow.Render(), Is.EqualTo(text));
            Assert.That(shadow.TryGetHash("player", out var hash), Is.True);
            Assert.That(hash, Is.EqualTo("cc$dd"));
        }

        [Test]
        public void Parse_skips_comments_and_blank_lines_and_keeps_the_last_duplicate()
        {
            var shadow = ShadowTable.Parse("# comment\n\nroot:aa$bb\nplayer:cc$dd\nplayer:ee$ff\n");

            Assert.That(shadow.Render(), Is.EqualTo("root:aa$bb\nplayer:ee$ff\n"));
            Assert.That(shadow.TryGetHash("player", out var hash), Is.True);
            Assert.That(hash, Is.EqualTo("ee$ff"));
        }

        [Test]
        public void SetHash_rejects_a_name_or_hash_that_could_forge_a_line()
        {
            var shadow = ShadowTable.Parse("root:aa$bb\n");

            Assert.Throws<ArgumentException>(() => shadow.SetHash("ro:ot", "cc$dd"));
            Assert.Throws<ArgumentException>(() => shadow.SetHash("player", "cc\ndd"));
            Assert.That(shadow.Render(), Is.EqualTo("root:aa$bb\n"));
        }
    }
}
