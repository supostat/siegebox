using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Siegebox.Scripting.Tests
{
    /// <summary>
    /// Pins the boot-safety contract: one broken disk mod — whatever the failure class —
    /// yields a failed result, rolls back its registrations and never stops the siblings.
    /// </summary>
    [TestFixture]
    public sealed class ModLoaderIsolationTests
    {
        private ModLoaderHarness harness;

        [SetUp]
        public void CreateHarness() => harness = new ModLoaderHarness();

        [TearDown]
        public void DisposeHarness() => harness.Dispose();

        [Test]
        public void Directory_without_manifest_fails_and_its_sibling_still_loads()
        {
            Directory.CreateDirectory(Path.Combine(harness.ModsRoot, "broken"));
            harness.WriteMod("good", "{\"id\": \"good\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            var broken = results.Single(result => result.ModId == "broken");
            Assert.That(broken.Loaded, Is.False);
            Assert.That(broken.Error, Does.Contain("manifest.json"));
            Assert.That(results.Single(result => result.ModId == "good").Loaded, Is.True);
        }

        [Test]
        public void Invalid_manifest_fails_the_mod()
        {
            harness.WriteMod("badjson", "{nope");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "badjson");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("json"));
        }

        [Test]
        public void Oversized_manifest_fails_the_mod()
        {
            var padding = new string(' ', ModLoader.MaxManifestBytes);
            harness.WriteMod("bloated", "{\"id\": \"bloated\", \"version\": \"1\"}" + padding);

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "bloated");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("manifest.json"));
        }

        [Test]
        public void Missing_script_file_fails_the_mod()
        {
            harness.WriteMod("ghost-script", "{\"id\": \"ghost-script\", \"version\": \"1\", \"scripts\": [\"ghost.lua\"]}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "ghost-script");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("ghost.lua"));
        }

        [Test]
        public void Oversized_script_fails_the_mod()
        {
            var padding = "--" + new string('x', ModLoader.MaxScriptBytes);
            harness.WriteMod(
                "heavy",
                "{\"id\": \"heavy\", \"version\": \"1\", \"scripts\": [\"big.lua\"]}",
                ("big.lua", padding));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "heavy");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("big.lua"));
        }

        [Test]
        public void Script_error_after_registration_rolls_the_mod_back()
        {
            harness.WriteMod(
                "faulty",
                "{\"id\": \"faulty\", \"version\": \"1\", \"scripts\": [\"setup.lua\"]}",
                ("setup.lua",
                    "siegebox.register_command('doomed', function(ctx) return 0 end)" +
                    " error('setup failed')"));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "faulty");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("setup failed"));
            Assert.That(harness.Commands.TryGet("doomed", out _), Is.False);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base" }));
        }

        [Test]
        public void Failed_file_type_registration_before_a_script_error_rolls_back_cleanly_and_siblings_load()
        {
            harness.WriteMod(
                "faulty",
                "{\"id\": \"faulty\", \"version\": \"1\", \"scripts\": [\"setup.lua\"]}",
                ("setup.lua",
                    "pcall(function() siegebox.register_file_type('txt', ' ') end)" +
                    " error('setup failed')"));
            harness.WriteMod(
                "sibling",
                "{\"id\": \"sibling\", \"version\": \"1\", \"scripts\": [\"install.lua\"]}",
                ("install.lua", "siegebox.register_command('siblingcmd', function(ctx) return 0 end)"));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "faulty");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("setup failed"));
            Assert.That(results.Single(result => result.ModId == "sibling").Loaded, Is.True);
            Assert.That(harness.Commands.TryGet("siblingcmd", out _), Is.True);
            Assert.That(harness.FileTypes.TryGet("txt", out _), Is.False);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base", "sibling" }));
        }

        [Test]
        public void Shadowing_a_base_command_fails_the_mod_and_keeps_the_base_command()
        {
            harness.WriteMod(
                "usurper",
                "{\"id\": \"usurper\", \"version\": \"1\", \"dependencies\": [\"base\"], \"scripts\": [\"shadow.lua\"]}",
                ("shadow.lua", "siegebox.register_command('nativecmd', function(ctx) return 0 end)"));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "usurper");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("nativecmd"));
            Assert.That(harness.Commands.TryGet("nativecmd", out _), Is.True);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base" }));
        }

        [Test]
        public void Script_listed_twice_runs_twice_so_a_registering_script_collides_with_itself()
        {
            harness.WriteMod(
                "twice",
                "{\"id\": \"twice\", \"version\": \"1\", \"scripts\": [\"setup.lua\", \"setup.lua\"]}",
                ("setup.lua", "siegebox.register_command('once', function(ctx) return 0 end)"));

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "twice");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("once"));
            Assert.That(harness.Commands.TryGet("once", out _), Is.False);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base" }));
        }

        [Test]
        public void Command_collision_between_two_mods_fails_the_later_and_keeps_the_first_intact()
        {
            harness.WriteMod(
                "alpha",
                "{\"id\": \"alpha\", \"version\": \"1\", \"scripts\": [\"a.lua\"]}",
                ("a.lua", "siegebox.register_command('twin', function(ctx) return 0 end)"));
            harness.WriteMod(
                "beta",
                "{\"id\": \"beta\", \"version\": \"1\", \"scripts\": [\"b.lua\"]}",
                ("b.lua",
                    "siegebox.register_command('extra', function(ctx) return 0 end)" +
                    " siegebox.register_command('twin', function(ctx) return 0 end)"));

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "alpha").Loaded, Is.True);
            var failed = results.Single(result => result.ModId == "beta");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("twin"));
            Assert.That(harness.Commands.TryGet("twin", out _), Is.True);
            Assert.That(harness.Commands.TryGet("extra", out _), Is.False);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base", "alpha" }));
        }
    }
}
