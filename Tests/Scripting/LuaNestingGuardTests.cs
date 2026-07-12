using System.Linq;
using NUnit.Framework;

namespace Siegebox.Scripting.Tests
{
    /// <summary>
    /// Pins the Lua-only recursion axes of the nesting guard: bracket-free block-keyword
    /// nesting, prefix unary-operator runs, long-bracket level matching and the script
    /// fail-closed variants. Sequential (non-nested) blocks and keyword-substring
    /// identifiers must still load.
    /// </summary>
    [TestFixture]
    public sealed class LuaNestingGuardTests
    {
        private ModLoaderHarness harness;

        [SetUp]
        public void CreateHarness() => harness = new ModLoaderHarness();

        [TearDown]
        public void DisposeHarness() => harness.Dispose();

        private void WriteScriptMod(string id, string script)
            => harness.WriteMod(
                id,
                $"{{\"id\": \"{id}\", \"version\": \"1\", \"scripts\": [\"{id}.lua\"]}}",
                ($"{id}.lua", script));

        [Test]
        public void Bracket_free_nested_blocks_fail_the_mod_instead_of_crashing()
        {
            WriteScriptMod("deepblocks", string.Concat(Enumerable.Repeat("do ", 250)) + string.Concat(Enumerable.Repeat("end ", 250)));
            harness.WriteMod("good", "{\"id\": \"good\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "deepblocks");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nesting"));
            Assert.That(results.Single(result => result.ModId == "good").Loaded, Is.True);
        }

        [Test]
        public void Prefix_unary_operator_run_fails_the_mod_instead_of_crashing()
        {
            WriteScriptMod("deepunary", "x = " + string.Concat(Enumerable.Repeat("not ", 250)) + "true");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "deepunary");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Block_nesting_at_the_cap_loads_and_one_over_fails()
        {
            const int maxDepth = 200;
            WriteScriptMod("blockcap", string.Concat(Enumerable.Repeat("do ", maxDepth)) + string.Concat(Enumerable.Repeat("end ", maxDepth)));
            WriteScriptMod("blockover", string.Concat(Enumerable.Repeat("do ", maxDepth + 1)) + string.Concat(Enumerable.Repeat("end ", maxDepth + 1)));

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "blockcap").Loaded, Is.True);
            var over = results.Single(result => result.ModId == "blockover");
            Assert.That(over.Loaded, Is.False);
            Assert.That(over.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Unary_run_at_the_cap_loads_and_one_over_fails()
        {
            const int maxDepth = 200;
            WriteScriptMod("unarycap", "x=" + string.Concat(Enumerable.Repeat("not ", maxDepth)) + "true");
            WriteScriptMod("unaryover", "x=" + string.Concat(Enumerable.Repeat("not ", maxDepth + 1)) + "true");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "unarycap").Loaded, Is.True);
            var over = results.Single(result => result.ModId == "unaryover");
            Assert.That(over.Loaded, Is.False);
            Assert.That(over.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Symbolic_unary_run_over_the_cap_fails()
        {
            WriteScriptMod("hashrun", "x=" + new string('#', 250) + "t");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "hashrun");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Sequential_blocks_and_keyword_substring_identifiers_load()
        {
            var blocks = string.Concat(Enumerable.Repeat("if x then end ", 300));
            WriteScriptMod("flat", "local endpoint = 1 local donut = 2 local functional = 3 " + blocks);

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "flat").Loaded, Is.True);
        }

        [Test]
        public void Spaced_binary_minus_between_operands_does_not_accumulate_a_unary_run()
        {
            WriteScriptMod("minus", "x = " + string.Join(" - ", Enumerable.Repeat("1", 400)) + " siegebox.register_command('minuscmd', function(ctx) return 0 end)");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "minus").Loaded, Is.True);
            Assert.That(harness.Commands.TryGet("minuscmd", out _), Is.True);
        }

        [Test]
        public void Unterminated_long_string_in_a_script_fails_closed()
        {
            WriteScriptMod("openlong", "x=[[" + new string('(', 500));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "openlong");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Unterminated_block_comment_in_a_script_fails_closed()
        {
            WriteScriptMod("opencomment", "--[[" + new string('(', 500));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "opencomment");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Unterminated_short_string_in_a_script_fails_closed()
        {
            WriteScriptMod("openshort", "x=\"" + new string('(', 500));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "openshort");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nesting"));
        }

        [Test]
        public void Level_one_long_string_ignores_a_bare_double_bracket_close()
        {
            WriteScriptMod("levelone", "x=[=[" + "]]" + new string('(', 300) + "]=] return 0");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "levelone").Loaded, Is.True);
        }

        [Test]
        public void Level_two_long_string_ignores_a_level_one_close()
        {
            WriteScriptMod("leveltwo", "x=[==[" + "]=]" + new string('(', 300) + "]==] return 0");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "leveltwo").Loaded, Is.True);
        }
    }
}
