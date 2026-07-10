using NUnit.Framework;
using Siegebox.Terminal;

namespace Siegebox.Terminal.Tests
{
    [TestFixture]
    public sealed class CommandHistoryTests
    {
        [Test]
        public void Up_navigates_from_newest_to_oldest_then_stops()
        {
            var history = new CommandHistory();
            history.Add("first");
            history.Add("second");

            Assert.That(history.TryMoveUp(out var newest), Is.True);
            Assert.That(newest, Is.EqualTo("second"));
            Assert.That(history.TryMoveUp(out var oldest), Is.True);
            Assert.That(oldest, Is.EqualTo("first"));
            Assert.That(history.TryMoveUp(out _), Is.False);
        }

        [Test]
        public void Down_past_the_newest_yields_empty_once()
        {
            var history = new CommandHistory();
            history.Add("first");
            history.Add("second");
            history.TryMoveUp(out _);
            history.TryMoveUp(out _);

            Assert.That(history.TryMoveDown(out var line), Is.True);
            Assert.That(line, Is.EqualTo("second"));
            Assert.That(history.TryMoveDown(out var blank), Is.True);
            Assert.That(blank, Is.EqualTo(""));
            Assert.That(history.TryMoveDown(out _), Is.False);
        }

        [Test]
        public void Fresh_history_has_nothing_to_navigate()
        {
            var history = new CommandHistory();

            Assert.That(history.TryMoveUp(out _), Is.False);
            Assert.That(history.TryMoveDown(out _), Is.False);
        }

        [Test]
        public void Blank_lines_are_ignored()
        {
            var history = new CommandHistory();
            history.Add("");
            history.Add("   ");

            Assert.That(history.TryMoveUp(out _), Is.False);
        }

        [Test]
        public void Consecutive_duplicates_collapse()
        {
            var history = new CommandHistory();
            history.Add("same");
            history.Add("same");

            Assert.That(history.TryMoveUp(out var line), Is.True);
            Assert.That(line, Is.EqualTo("same"));
            Assert.That(history.TryMoveUp(out _), Is.False);
        }

        [Test]
        public void Oldest_entry_is_evicted_beyond_the_cap()
        {
            var history = new CommandHistory();
            for (var index = 0; index <= CommandHistory.MaxEntries; index++)
            {
                history.Add("cmd" + index);
            }

            var reached = "";
            while (history.TryMoveUp(out var line))
            {
                reached = line;
            }

            Assert.That(reached, Is.EqualTo("cmd1"));
        }

        [Test]
        public void Add_resets_navigation_to_the_newest()
        {
            var history = new CommandHistory();
            history.Add("first");
            history.TryMoveUp(out _);

            history.Add("second");

            Assert.That(history.TryMoveUp(out var line), Is.True);
            Assert.That(line, Is.EqualTo("second"));
        }
    }
}
