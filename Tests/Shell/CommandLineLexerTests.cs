using System.Linq;
using NUnit.Framework;
using Siegebox.Shell;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class CommandLineLexerTests
    {
        private static Token[] Lex(string line) => new CommandLineLexer().Tokenize(line).ToArray();

        [Test]
        public void Words_and_operators_carry_their_positions()
        {
            var tokens = Lex("echo hi >> out");

            Assert.That(tokens.Select(token => token.Type), Is.EqualTo(new[]
            {
                TokenType.Word, TokenType.Word, TokenType.RedirectAppend, TokenType.Word
            }));
            Assert.That(tokens.Select(token => token.Text), Is.EqualTo(new[] { "echo", "hi", ">>", "out" }));
            Assert.That(tokens.Select(token => token.Position), Is.EqualTo(new[] { 0, 5, 8, 11 }));
        }

        [Test]
        public void Maximal_munch_distinguishes_every_operator()
        {
            var tokens = Lex("a|b>c>>d<e&f;g&&h||i");

            Assert.That(tokens.Select(token => token.Type), Is.EqualTo(new[]
            {
                TokenType.Word, TokenType.Pipe,
                TokenType.Word, TokenType.RedirectOut,
                TokenType.Word, TokenType.RedirectAppend,
                TokenType.Word, TokenType.RedirectIn,
                TokenType.Word, TokenType.Background,
                TokenType.Word, TokenType.Semicolon,
                TokenType.Word, TokenType.AndIf,
                TokenType.Word, TokenType.OrIf,
                TokenType.Word
            }));
            Assert.That(tokens.Select(token => token.Text), Is.EqualTo(new[]
            {
                "a", "|", "b", ">", "c", ">>", "d", "<", "e", "&", "f", ";", "g", "&&", "h", "||", "i"
            }));
        }

        [Test]
        public void Quoted_operator_characters_stay_inside_the_word()
        {
            var tokens = Lex("echo \"a|b\" 'c>d'");

            Assert.That(tokens.Select(token => token.Type), Is.All.EqualTo(TokenType.Word));
            Assert.That(tokens.Select(token => token.Text), Is.EqualTo(new[] { "echo", "\"a|b\"", "'c>d'" }));
        }

        [Test]
        public void Escaped_operator_character_stays_inside_the_word()
        {
            var tokens = Lex("a\\|b");

            Assert.That(tokens.Length, Is.EqualTo(1));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Word));
            Assert.That(tokens[0].Text, Is.EqualTo("a\\|b"));
        }

        [Test]
        public void Empty_and_blank_lines_yield_no_tokens()
        {
            Assert.That(Lex(""), Is.Empty);
            Assert.That(Lex("   \t  "), Is.Empty);
        }

        [Test]
        public void Unterminated_single_quote_reports_the_quote_position()
        {
            var error = Assert.Throws<ShellParseException>(() => Lex("echo 'oops"));

            Assert.That(error.Position, Is.EqualTo(5));
        }

        [Test]
        public void Unterminated_double_quote_reports_the_quote_position()
        {
            var error = Assert.Throws<ShellParseException>(() => Lex("echo ab\"oops"));

            Assert.That(error.Position, Is.EqualTo(7));
        }

        [Test]
        public void Escaped_double_quote_does_not_terminate_the_span()
        {
            var tokens = Lex("echo \"a\\\"b\"");

            Assert.That(tokens.Length, Is.EqualTo(2));
            Assert.That(tokens[1].Text, Is.EqualTo("\"a\\\"b\""));
        }
    }
}
