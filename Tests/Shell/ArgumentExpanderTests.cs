using NUnit.Framework;
using Siegebox.Shell;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class ArgumentExpanderTests
    {
        private static readonly ArgumentExpander Expander = new ArgumentExpander();

        [Test]
        public void Double_quoted_span_becomes_a_single_argument()
        {
            Assert.That(Expander.ExpandWord("\"a b\""), Is.EqualTo("a b"));
        }

        [Test]
        public void Single_quoted_span_is_literal()
        {
            Assert.That(Expander.ExpandWord("'x y'"), Is.EqualTo("x y"));
            Assert.That(Expander.ExpandWord("'a\\nb'"), Is.EqualTo("a\\nb"));
        }

        [Test]
        public void Adjacent_quoted_and_bare_segments_concatenate()
        {
            Assert.That(Expander.ExpandWord("a\"b c\"d"), Is.EqualTo("ab cd"));
        }

        [Test]
        public void Empty_quotes_yield_an_empty_argument()
        {
            Assert.That(Expander.ExpandWord("\"\""), Is.EqualTo(""));
            Assert.That(Expander.Expand(new[] { "echo", "\"\"" }), Is.EqualTo(new[] { "echo", "" }));
        }

        [Test]
        public void Variables_and_globs_pass_through_unexpanded()
        {
            Assert.That(Expander.ExpandWord("$HOME"), Is.EqualTo("$HOME"));
            Assert.That(Expander.ExpandWord("*.txt"), Is.EqualTo("*.txt"));
        }

        [Test]
        public void Backslash_escape_is_removed_outside_single_quotes()
        {
            Assert.That(Expander.ExpandWord("\\\""), Is.EqualTo("\""));
            Assert.That(Expander.ExpandWord("\"a\\\"b\""), Is.EqualTo("a\"b"));
            Assert.That(Expander.ExpandWord("a\\|b"), Is.EqualTo("a|b"));
        }

        [Test]
        public void Expand_preserves_word_order()
        {
            Assert.That(
                Expander.Expand(new[] { "echo", "\"a b\"", "'c'" }),
                Is.EqualTo(new[] { "echo", "a b", "c" }));
        }
    }
}
