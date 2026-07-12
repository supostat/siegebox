using System.Linq;
using NUnit.Framework;

namespace Siegebox.Scripting.Tests
{
    /// <summary>
    /// Pins the disk-boundary nesting guard: deeply nested or lexically deceptive manifests
    /// and scripts must yield a failed ModLoadResult instead of crashing the recursive
    /// parsers, and siblings must still load. The Lua guard is lexer-aware — comments and
    /// long strings cannot smuggle deep nesting past the scanner.
    /// </summary>
    [TestFixture]
    public sealed class NestingGuardTests
    {
        private ModLoaderHarness harness;

        [SetUp]
        public void CreateHarness() => harness = new ModLoaderHarness();

        [TearDown]
        public void DisposeHarness() => harness.Dispose();

        [Test]
        public void Deeply_nested_manifest_fails_the_mod_instead_of_crashing_the_parser()
        {
            harness.WriteMod("deepjson", "{\"a\": " + new string('[', 30_000));
            harness.WriteMod("good", "{\"id\": \"good\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "deepjson");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("manifest.json"));
            Assert.That(failed.Error, Does.Contain("nesting"));
            Assert.That(results.Single(result => result.ModId == "good").Loaded, Is.True);
        }

        [Test]
        public void Deeply_nested_script_fails_the_mod_instead_of_crashing_the_parser()
        {
            harness.WriteMod(
                "deeplua",
                "{\"id\": \"deeplua\", \"version\": \"1\", \"scripts\": [\"deep.lua\"]}",
                ("deep.lua", "return " + new string('(', 100_000)));
            harness.WriteMod("good", "{\"id\": \"good\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "deeplua");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("deep.lua"));
            Assert.That(failed.Error, Does.Contain("nesting"));
            Assert.That(results.Single(result => result.ModId == "good").Loaded, Is.True);
        }

        [Test]
        public void Line_comment_with_a_stray_quote_cannot_hide_deep_nesting()
        {
            harness.WriteMod(
                "bypass",
                "{\"id\": \"bypass\", \"version\": \"1\", \"scripts\": [\"bypass.lua\"]}",
                ("bypass.lua", "-- '\n" + new string('(', 100_000)));
            harness.WriteMod("good", "{\"id\": \"good\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "bypass");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("bypass.lua"));
            Assert.That(failed.Error, Does.Contain("nesting"));
            Assert.That(results.Single(result => result.ModId == "good").Loaded, Is.True);
        }

        [Test]
        public void Long_string_with_a_stray_quote_cannot_hide_deep_nesting()
        {
            harness.WriteMod(
                "longstr",
                "{\"id\": \"longstr\", \"version\": \"1\", \"scripts\": [\"longstr.lua\"]}",
                ("longstr.lua", "x=[[']]" + new string('(', 100_000)));
            harness.WriteMod("good", "{\"id\": \"good\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "longstr");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("longstr.lua"));
            Assert.That(failed.Error, Does.Contain("nesting"));
            Assert.That(results.Single(result => result.ModId == "good").Loaded, Is.True);
        }

        [Test]
        public void Manifest_unterminated_string_fails_closed_as_nesting_before_json_parse()
        {
            harness.WriteMod("unterminated", "{\"a\": \"" + new string('[', 500));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "unterminated");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("manifest.json"));
            Assert.That(failed.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Exactly_the_maximum_depth_loads_and_one_over_fails()
        {
            const int maxDepth = 200;
            harness.WriteMod(
                "atcap",
                "{\"id\": \"atcap\", \"version\": \"1\", \"scripts\": [\"cap.lua\"]}",
                ("cap.lua", "x=" + new string('(', maxDepth) + "0" + new string(')', maxDepth)));
            harness.WriteMod(
                "overcap",
                "{\"id\": \"overcap\", \"version\": \"1\", \"scripts\": [\"over.lua\"]}",
                ("over.lua", "x=" + new string('(', maxDepth + 1) + "0" + new string(')', maxDepth + 1)));

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "atcap").Loaded, Is.True);
            var over = results.Single(result => result.ModId == "overcap");
            Assert.That(over.Loaded, Is.False);
            Assert.That(over.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Double_quoted_brackets_in_a_manifest_string_do_not_count()
        {
            var bracketsInsideString = new string('[', 300) + new string('(', 300);
            harness.WriteMod("stringy", $"{{\"id\": \"stringy\", \"version\": \"{bracketsInsideString}\"}}");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "stringy").Loaded, Is.True);
        }

        [Test]
        public void Escaped_quote_inside_a_manifest_string_still_loads()
        {
            harness.WriteMod("escaped", "{\"id\": \"escaped\", \"version\": \"a\\\"b\", \"dependencies\": []}");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "escaped").Loaded, Is.True);
        }

        [Test]
        public void Brackets_inside_a_single_quoted_lua_string_do_not_count()
        {
            harness.WriteMod(
                "quoted",
                "{\"id\": \"quoted\", \"version\": \"1\", \"scripts\": [\"quoted.lua\"]}",
                ("quoted.lua", "x='" + new string('(', 300) + "' siegebox.register_command('quotedcmd', function(ctx) return 0 end)"));

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "quoted").Loaded, Is.True);
            Assert.That(harness.Commands.TryGet("quotedcmd", out _), Is.True);
        }
    }
}
