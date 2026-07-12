using System;
using NUnit.Framework;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class ModManifestTests
    {
        [Test]
        public void Full_manifest_parses()
        {
            var manifest = ModManifest.Parse(
                "{\"id\": \"example\", \"version\": \"0.1.0\", \"dependencies\": [\"base\"]," +
                " \"loadOrder\": 100, \"scripts\": [\"hello_command.lua\", \"hello_app.lua\"]}");

            Assert.That(manifest.Id, Is.EqualTo("example"));
            Assert.That(manifest.Version, Is.EqualTo("0.1.0"));
            Assert.That(manifest.Dependencies, Is.EqualTo(new[] { "base" }));
            Assert.That(manifest.LoadOrder, Is.EqualTo(100));
            Assert.That(manifest.Scripts, Is.EqualTo(new[] { "hello_command.lua", "hello_app.lua" }));
        }

        [Test]
        public void Omitted_optionals_default()
        {
            var manifest = ModManifest.Parse("{\"id\": \"example\", \"version\": \"0.1.0\"}");

            Assert.That(manifest.Dependencies, Is.Empty);
            Assert.That(manifest.LoadOrder, Is.EqualTo(0));
            Assert.That(manifest.Scripts, Is.Empty);
        }

        [Test]
        public void Missing_id_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(() => ModManifest.Parse("{\"version\": \"0.1.0\"}"));
            Assert.That(error.Message, Does.Contain("id"));
        }

        [Test]
        public void Missing_version_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(() => ModManifest.Parse("{\"id\": \"example\"}"));
            Assert.That(error.Message, Does.Contain("version"));
        }

        [TestCase("Bad Id")]
        [TestCase("UPPER")]
        [TestCase("1leading-digit")]
        [TestCase("")]
        public void Invalid_id_is_rejected(string id)
        {
            var error = Assert.Throws<ModLoadException>(
                () => ModManifest.Parse($"{{\"id\": \"{id}\", \"version\": \"0.1.0\"}}"));
            Assert.That(error.Message, Does.Contain("id"));
        }

        [Test]
        public void Sixty_four_character_id_is_accepted_and_sixty_five_is_rejected()
        {
            var longestValidId = "a" + new string('b', 63);
            var oneOverTheCap = longestValidId + "b";

            var manifest = ModManifest.Parse($"{{\"id\": \"{longestValidId}\", \"version\": \"1\"}}");
            Assert.That(manifest.Id, Is.EqualTo(longestValidId));

            var error = Assert.Throws<ModLoadException>(
                () => ModManifest.Parse($"{{\"id\": \"{oneOverTheCap}\", \"version\": \"1\"}}"));
            Assert.That(error.Message, Does.Contain("id"));
        }

        [TestCase("{\"id\": null, \"version\": \"1\"}", "id")]
        [TestCase("{\"id\": \"example\", \"version\": null}", "version")]
        [TestCase("{\"id\": \"example\", \"version\": \"1\", \"dependencies\": null}", "dependencies")]
        [TestCase("{\"id\": \"example\", \"version\": \"1\", \"loadOrder\": null}", "loadOrder")]
        [TestCase("{\"id\": \"example\", \"version\": \"1\", \"scripts\": null}", "scripts")]
        public void Json_null_field_is_rejected_naming_the_field(string json, string fieldName)
        {
            var error = Assert.Throws<ModLoadException>(() => ModManifest.Parse(json));
            Assert.That(error.Message, Does.Contain(fieldName));
        }

        [Test]
        public void Non_string_script_entry_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(
                () => ModManifest.Parse("{\"id\": \"example\", \"version\": \"0.1.0\", \"scripts\": [42]}"));
            Assert.That(error.Message, Does.Contain("scripts"));
        }

        [TestCase("../evil.lua")]
        [TestCase("/abs.lua")]
        [TestCase("a/b.lua")]
        public void Script_entries_with_path_segments_are_rejected(string script)
        {
            var error = Assert.Throws<ModLoadException>(
                () => ModManifest.Parse($"{{\"id\": \"example\", \"version\": \"0.1.0\", \"scripts\": [\"{script}\"]}}"));
            Assert.That(error.Message, Does.Contain("scripts"));
        }

        [Test]
        public void Script_entry_with_a_backslash_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(
                () => new ModManifest("example", "0.1.0", Array.Empty<string>(), 0, new[] { "a\\b.lua" }));
            Assert.That(error.Message, Does.Contain("scripts"));
        }

        [Test]
        public void Invalid_dependency_id_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(
                () => ModManifest.Parse("{\"id\": \"example\", \"version\": \"0.1.0\", \"dependencies\": [\"Bad Dep\"]}"));
            Assert.That(error.Message, Does.Contain("dependencies"));
        }

        [Test]
        public void Non_integer_load_order_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(
                () => ModManifest.Parse("{\"id\": \"example\", \"version\": \"0.1.0\", \"loadOrder\": 1.5}"));
            Assert.That(error.Message, Does.Contain("loadOrder"));
        }

        [Test]
        public void Invalid_json_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(() => ModManifest.Parse("{nope"));
            Assert.That(error.Message, Does.Contain("json"));
        }

        [Test]
        public void Non_object_json_is_rejected()
        {
            var error = Assert.Throws<ModLoadException>(() => ModManifest.Parse("\"just a string\""));
            Assert.That(error.Message, Does.Contain("json"));
        }

        [Test]
        public void Constructor_applies_the_same_validation()
        {
            Assert.Throws<ModLoadException>(
                () => new ModManifest("Bad Id", "0.1.0", Array.Empty<string>(), 0, Array.Empty<string>()));
            Assert.Throws<ModLoadException>(
                () => new ModManifest("example", " ", Array.Empty<string>(), 0, Array.Empty<string>()));
            Assert.Throws<ArgumentNullException>(
                () => new ModManifest(null, "0.1.0", Array.Empty<string>(), 0, Array.Empty<string>()));
        }
    }
}
