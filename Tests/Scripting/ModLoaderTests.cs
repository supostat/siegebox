using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class ModLoaderTests
    {
        private ModLoaderHarness harness;

        [SetUp]
        public void CreateHarness() => harness = new ModLoaderHarness();

        [TearDown]
        public void DisposeHarness() => harness.Dispose();

        [Test]
        public void Native_base_loads_first_and_disk_registrations_are_usable()
        {
            harness.WriteMod(
                "example",
                "{\"id\": \"example\", \"version\": \"0.1.0\", \"dependencies\": [\"base\"], \"scripts\": [\"hello.lua\"]}",
                ("hello.lua",
                    "siegebox.register_command('hello', function(ctx) ctx.write('hi') return 0 end)" +
                    " siegebox.register_app{ id = 'hello-app', name = 'hello', on_launch = function(app) app.set_text('up') end }"));

            var results = harness.LoadAll();

            Assert.That(results.All(result => result.Loaded), Is.True);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base", "example" }));
            Assert.That(harness.Commands.TryGet("nativecmd", out _), Is.True);
            Assert.That(harness.Commands.TryGet("hello", out _), Is.True);
            Assert.That(harness.Apps.TryGet("hello-app", out _), Is.True);
        }

        [Test]
        public void Disk_mods_load_by_load_order_then_id()
        {
            harness.WriteMod("dir-a", "{\"id\": \"delta\", \"version\": \"1\", \"loadOrder\": 10}");
            harness.WriteMod("dir-b", "{\"id\": \"alpha\", \"version\": \"1\", \"loadOrder\": 10}");
            harness.WriteMod("dir-c", "{\"id\": \"zulu\", \"version\": \"1\", \"loadOrder\": 5}");

            harness.LoadAll();

            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base", "zulu", "alpha", "delta" }));
        }

        [Test]
        public void Missing_root_loads_natives_only()
        {
            var results = harness.Loader.LoadAll(Path.Combine(harness.ModsRoot, "absent"));

            Assert.That(results, Has.Count.EqualTo(1));
            Assert.That(results[0].ModId, Is.EqualTo("base"));
            Assert.That(results[0].Loaded, Is.True);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base" }));
        }

        [Test]
        public void Missing_dependency_skips_the_mod()
        {
            harness.WriteMod("orphan", "{\"id\": \"orphan\", \"version\": \"1\", \"dependencies\": [\"ghost\"]}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "orphan");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("ghost"));
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base" }));
        }

        [Test]
        public void Mod_depending_on_itself_fails_as_not_loaded()
        {
            harness.WriteMod("selfy", "{\"id\": \"selfy\", \"version\": \"1\", \"dependencies\": [\"selfy\"]}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "selfy");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("'selfy'"));
            Assert.That(failed.Error, Does.Contain("is not loaded"));
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base" }));
        }

        [Test]
        public void Dependency_on_a_sibling_with_higher_load_order_fails_as_not_loaded()
        {
            harness.WriteMod("early", "{\"id\": \"early\", \"version\": \"1\", \"loadOrder\": 1, \"dependencies\": [\"late\"]}");
            harness.WriteMod("late", "{\"id\": \"late\", \"version\": \"1\", \"loadOrder\": 2}");

            var results = harness.LoadAll();

            var failed = results.Single(result => result.ModId == "early");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("'late'"));
            Assert.That(failed.Error, Does.Contain("is not loaded"));
            Assert.That(results.Single(result => result.ModId == "late").Loaded, Is.True);
            Assert.That(harness.Loader.LoadedModIds, Is.EqualTo(new[] { "base", "late" }));
        }

        [Test]
        public void Duplicate_id_fails_the_later_directory()
        {
            harness.WriteMod("aaa", "{\"id\": \"twin\", \"version\": \"1\"}");
            harness.WriteMod("bbb", "{\"id\": \"twin\", \"version\": \"1\"}");

            var results = harness.LoadAll();

            Assert.That(results.Single(result => result.ModId == "twin").Loaded, Is.True);
            var failed = results.Single(result => result.ModId == "bbb");
            Assert.That(failed.Loaded, Is.False);
            Assert.That(failed.Error, Does.Contain("twin"));
        }

        [Test]
        public void Duplicate_native_id_is_rejected_at_registration()
        {
            Assert.Throws<ArgumentException>(() => harness.Loader.RegisterNative(
                new ModManifest("base", "0.2.0", Array.Empty<string>(), 0, Array.Empty<string>()),
                () => { }));
        }

        [Test]
        public void Failing_native_install_fails_fast()
        {
            harness.Loader.RegisterNative(
                new ModManifest("cursed", "1", Array.Empty<string>(), 0, Array.Empty<string>()),
                () => throw new InvalidOperationException("native boom"));

            Assert.Throws<InvalidOperationException>(() => harness.LoadAll());
        }
    }
}
