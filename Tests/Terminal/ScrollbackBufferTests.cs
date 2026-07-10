using System.Text;
using NUnit.Framework;
using Siegebox.Shell;
using Siegebox.Terminal;

namespace Siegebox.Terminal.Tests
{
    [TestFixture]
    public sealed class ScrollbackBufferTests
    {
        [Test]
        public void Append_accumulates_text_and_increments_the_version()
        {
            var scrollback = new ScrollbackBuffer();
            var initialVersion = scrollback.Version;

            scrollback.Append("one\n");
            scrollback.Append("two\n");

            Assert.That(scrollback.Text, Is.EqualTo("one\ntwo\n"));
            Assert.That(scrollback.Version, Is.GreaterThan(initialVersion));
        }

        [Test]
        public void Append_of_an_empty_chunk_is_a_no_op()
        {
            var scrollback = new ScrollbackBuffer();
            scrollback.Append("content");
            var versionBefore = scrollback.Version;

            scrollback.Append("");

            Assert.That(scrollback.Version, Is.EqualTo(versionBefore));
            Assert.That(scrollback.Text, Is.EqualTo("content"));
        }

        [Test]
        public void Head_trim_drops_whole_lines()
        {
            var scrollback = new ScrollbackBuffer();
            var line = "123456789\n";
            var content = new StringBuilder();
            for (var index = 0; index < 7000; index++)
            {
                content.Append(line);
            }

            scrollback.Append(content.ToString());

            Assert.That(scrollback.Text.Length, Is.LessThanOrEqualTo(ScrollbackBuffer.MaxCharacters));
            Assert.That(scrollback.Text.Length % line.Length, Is.EqualTo(0));
            Assert.That(scrollback.Text, Does.StartWith(line));
        }

        [Test]
        public void Head_trim_keeps_the_newest_tail_intact()
        {
            var scrollback = new ScrollbackBuffer();
            for (var index = 0; index < 8000; index++)
            {
                scrollback.Append($"line-{index:D6}\n");
            }

            Assert.That(scrollback.Text, Does.EndWith("line-007999\n"));
            Assert.That(scrollback.Text, Does.StartWith("line-"));
            Assert.That(scrollback.Text.Length, Is.LessThanOrEqualTo(ScrollbackBuffer.MaxCharacters));
        }

        [Test]
        public void Single_over_cap_line_is_hard_cut()
        {
            var scrollback = new ScrollbackBuffer();

            scrollback.Append(new string('a', ScrollbackBuffer.MaxCharacters + 5000));

            Assert.That(scrollback.Text.Length, Is.EqualTo(ScrollbackBuffer.MaxCharacters));
        }

        [Test]
        public void Clear_sequence_drops_everything_through_it()
        {
            var scrollback = new ScrollbackBuffer();

            scrollback.Append("before\n" + ClearCommand.ClearSequence + "after");

            Assert.That(scrollback.Text, Is.EqualTo("after"));
        }

        [Test]
        public void Clear_sequence_split_across_two_appends_is_detected()
        {
            var scrollback = new ScrollbackBuffer();
            var sequence = ClearCommand.ClearSequence;

            scrollback.Append("x" + sequence.Substring(0, 3));
            scrollback.Append(sequence.Substring(3) + "y");

            Assert.That(scrollback.Text, Is.EqualTo("y"));
        }

        [Test]
        public void Clear_empties_the_buffer_and_bumps_the_version()
        {
            var scrollback = new ScrollbackBuffer();
            scrollback.Append("content");
            var versionBeforeClear = scrollback.Version;

            scrollback.Clear();

            Assert.That(scrollback.Text, Is.EqualTo(""));
            Assert.That(scrollback.Version, Is.GreaterThan(versionBeforeClear));
        }
    }
}
