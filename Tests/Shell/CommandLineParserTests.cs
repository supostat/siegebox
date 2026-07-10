using NUnit.Framework;
using Siegebox.Shell;

namespace Siegebox.Shell.Tests
{
    [TestFixture]
    public sealed class CommandLineParserTests
    {
        private static ListNode Parse(string line)
            => new CommandLineParser().Parse(new CommandLineLexer().Tokenize(line));

        [Test]
        public void Pipeline_with_redirect_and_and_if_builds_the_expected_tree()
        {
            var list = Parse("a | b > c && d");

            Assert.That(list.Items.Count, Is.EqualTo(2));

            var first = list.Items[0];
            Assert.That(first.Operator, Is.EqualTo(ListOperator.Always));
            Assert.That(first.Pipeline.Background, Is.False);
            Assert.That(first.Pipeline.Commands.Count, Is.EqualTo(2));
            Assert.That(first.Pipeline.Commands[0].Words, Is.EqualTo(new[] { "a" }));
            Assert.That(first.Pipeline.Commands[1].Words, Is.EqualTo(new[] { "b" }));
            Assert.That(first.Pipeline.Commands[1].Redirections.Count, Is.EqualTo(1));
            Assert.That(first.Pipeline.Commands[1].Redirections[0].Kind, Is.EqualTo(RedirectionKind.Out));
            Assert.That(first.Pipeline.Commands[1].Redirections[0].TargetWord, Is.EqualTo("c"));

            var second = list.Items[1];
            Assert.That(second.Operator, Is.EqualTo(ListOperator.AndIf));
            Assert.That(second.Pipeline.Commands[0].Words, Is.EqualTo(new[] { "d" }));
        }

        [Test]
        public void Ampersand_marks_the_preceding_pipeline_as_background()
        {
            var list = Parse("a &");

            Assert.That(list.Items.Count, Is.EqualTo(1));
            Assert.That(list.Items[0].Pipeline.Background, Is.True);
        }

        [Test]
        public void Ampersand_separates_two_pipelines()
        {
            var list = Parse("a & b");

            Assert.That(list.Items.Count, Is.EqualTo(2));
            Assert.That(list.Items[0].Pipeline.Background, Is.True);
            Assert.That(list.Items[1].Pipeline.Background, Is.False);
            Assert.That(list.Items[1].Operator, Is.EqualTo(ListOperator.Always));
        }

        [Test]
        public void Redirections_are_kept_in_order_with_standard_descriptors()
        {
            var list = Parse("x < in > out");

            var command = list.Items[0].Pipeline.Commands[0];
            Assert.That(command.Words, Is.EqualTo(new[] { "x" }));
            Assert.That(command.Redirections.Count, Is.EqualTo(2));
            Assert.That(command.Redirections[0].Kind, Is.EqualTo(RedirectionKind.In));
            Assert.That(command.Redirections[0].TargetWord, Is.EqualTo("in"));
            Assert.That(command.Redirections[0].Descriptor, Is.EqualTo(0));
            Assert.That(command.Redirections[1].Kind, Is.EqualTo(RedirectionKind.Out));
            Assert.That(command.Redirections[1].TargetWord, Is.EqualTo("out"));
            Assert.That(command.Redirections[1].Descriptor, Is.EqualTo(1));
        }

        [Test]
        public void Duplicate_redirections_are_kept_in_order()
        {
            var list = Parse("x > first > second");

            var redirections = list.Items[0].Pipeline.Commands[0].Redirections;
            Assert.That(redirections.Count, Is.EqualTo(2));
            Assert.That(redirections[0].TargetWord, Is.EqualTo("first"));
            Assert.That(redirections[1].TargetWord, Is.EqualTo("second"));
        }

        [Test]
        public void Trailing_semicolon_is_allowed()
        {
            var list = Parse("a ;");

            Assert.That(list.Items.Count, Is.EqualTo(1));
            Assert.That(list.Items[0].Pipeline.Commands[0].Words, Is.EqualTo(new[] { "a" }));
        }

        [Test]
        public void Pipe_without_a_preceding_command_is_an_error()
        {
            Assert.Throws<ShellParseException>(() => Parse("| a"));
        }

        [Test]
        public void Dangling_and_if_is_an_error()
        {
            Assert.Throws<ShellParseException>(() => Parse("a &&"));
        }

        [Test]
        public void Redirect_without_a_target_is_an_error()
        {
            Assert.Throws<ShellParseException>(() => Parse("a > "));
        }

        [Test]
        public void Double_pipe_with_a_gap_is_an_error()
        {
            Assert.Throws<ShellParseException>(() => Parse("a | | b"));
        }
    }
}
