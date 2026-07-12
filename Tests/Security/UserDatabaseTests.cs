using NUnit.Framework;

namespace Siegebox.Security.Tests
{
    /// <summary>
    /// Pins the pure parsers for the user db (passwd + shadow) and the salted-SHA-256
    /// password hashing: valid content round-trips, malformed lines are rejected, and a
    /// hash verifies only against the password that made it.
    /// </summary>
    [TestFixture]
    public sealed class UserDatabaseTests
    {
        [Test]
        public void UserDatabase_parses_passwd_and_shadow()
        {
            var database = UserDatabase.Parse("# users\n\nroot:0:0:/root\nplayer:1000:1000:/home/player\n");
            var shadow = ShadowTable.Parse("root:aa$bb\nplayer:cc$dd\n");

            Assert.That(database.Records, Has.Count.EqualTo(2));
            Assert.That(database.TryGetByName("player", out var player), Is.True);
            Assert.That(player.Uid, Is.EqualTo(1000));
            Assert.That(player.Gid, Is.EqualTo(1000));
            Assert.That(player.Home, Is.EqualTo("/home/player"));
            Assert.That(database.TryGetByUid(0, out var root), Is.True);
            Assert.That(root.Name, Is.EqualTo("root"));
            Assert.That(shadow.TryGetHash("player", out var hash), Is.True);
            Assert.That(hash, Is.EqualTo("cc$dd"));
        }

        [TestCase("player:notanumber:1000:/home")]
        [TestCase("player:1000:notanumber:/home")]
        [TestCase("player:1000:1000")]
        [TestCase("player:1000:1000:/home:extra")]
        [TestCase("player:1000:1000:relative")]
        [TestCase(":0:0:/root")]
        [TestCase("player:-1:1000:/home")]
        public void UserDatabase_rejects_a_malformed_passwd_line(string content)
        {
            Assert.That(() => UserDatabase.Parse(content), Throws.InstanceOf<UserDatabaseException>());
        }

        [TestCase("noseparator")]
        [TestCase("name:a:b")]
        [TestCase("name:")]
        [TestCase(":hash")]
        public void ShadowTable_rejects_a_malformed_line(string content)
        {
            Assert.That(() => ShadowTable.Parse(content), Throws.InstanceOf<UserDatabaseException>());
        }

        [Test]
        public void Password_hash_verifies_only_the_original_password()
        {
            var stored = PasswordHash.Create("hunter2");

            Assert.That(PasswordHash.Verify("hunter2", stored), Is.True);
            Assert.That(PasswordHash.Verify("hunter3", stored), Is.False);
            Assert.That(PasswordHash.Verify("", stored), Is.False);
        }

        [Test]
        public void Password_hash_salts_each_call_so_equal_passwords_differ()
        {
            Assert.That(PasswordHash.Create("same"), Is.Not.EqualTo(PasswordHash.Create("same")));
        }

        [TestCase("")]
        [TestCase("nodollar")]
        [TestCase("$onlyhash")]
        public void Password_hash_rejects_a_malformed_stored_value(string stored)
        {
            Assert.That(PasswordHash.Verify("whatever", stored), Is.False);
        }
    }
}
