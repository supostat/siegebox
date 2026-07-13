using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using NUnit.Framework;
using Siegebox.Vfs;

namespace Siegebox.Persistence.Tests
{
    /// <summary>
    /// Pins the end-to-end save round-trip: <see cref="SaveSerializer.Capture"/> stamps the
    /// current version, the whole <see cref="SaveGame"/> survives a JSON codec pass, and
    /// <see cref="SaveSerializer.Load"/> yields a usable VFS plus the window layout verbatim.
    /// Also pins that the JSON codec — not the in-process import depth cap — is the real
    /// untrusted-input DoS boundary: an over-deep JSON string is rejected at parse time,
    /// before Load ever sees a tree.
    /// </summary>
    [TestFixture]
    public sealed class SaveGameRoundTripTests
    {
        private static readonly Credentials Root = new Credentials(0);

        private static PermissionMode Mode(int bits) => new PermissionMode(bits);

        [Test]
        public void Capture_stamps_the_current_version_and_round_trips_the_whole_save()
        {
            var vfs = new VirtualFileSystem();
            vfs.CreateDirectory("/home", Mode(0b111_101_101), Root);
            vfs.CreateFile("/home/note.txt", Mode(0b110_100_100), Root);
            Write(vfs, "/home/note.txt", "persist");

            var windows = new List<WindowSnapshot>
            {
                new WindowSnapshot
                {
                    AppId = "terminal",
                    X = 10f,
                    Y = 20f,
                    Width = 640f,
                    Height = 420f,
                    State = WindowDisplayState.Normal,
                    ZOrderIndex = 0,
                    Focused = true,
                    AppState = "session-blob"
                },
                new WindowSnapshot
                {
                    AppId = "files",
                    X = 34f,
                    Y = 44f,
                    Width = 500f,
                    Height = 300f,
                    State = WindowDisplayState.Minimized,
                    ZOrderIndex = 1,
                    Focused = false,
                    AppState = null
                }
            };

            var save = SaveSerializer.Capture(vfs.Export(), windows);
            Assert.That(save.Version, Is.EqualTo(SaveVersion.Current));

            var json = JsonSerializer.Serialize(save);
            var restoredSave = JsonSerializer.Deserialize<SaveGame>(json);
            var loaded = SaveSerializer.Load(restoredSave);

            Assert.That(Read(loaded.Vfs, "/home/note.txt"), Is.EqualTo("persist"));
            Assert.That(loaded.Windows.Count, Is.EqualTo(2));
            Assert.That(loaded.Windows[0].AppId, Is.EqualTo("terminal"));
            Assert.That(loaded.Windows[0].X, Is.EqualTo(10f));
            Assert.That(loaded.Windows[0].Y, Is.EqualTo(20f));
            Assert.That(loaded.Windows[0].Width, Is.EqualTo(640f));
            Assert.That(loaded.Windows[0].Height, Is.EqualTo(420f));
            Assert.That(loaded.Windows[0].State, Is.EqualTo(WindowDisplayState.Normal));
            Assert.That(loaded.Windows[0].ZOrderIndex, Is.EqualTo(0));
            Assert.That(loaded.Windows[0].Focused, Is.True);
            Assert.That(loaded.Windows[0].AppState, Is.EqualTo("session-blob"));
            Assert.That(loaded.Windows[1].State, Is.EqualTo(WindowDisplayState.Minimized));
            Assert.That(loaded.Windows[1].ZOrderIndex, Is.EqualTo(1));
            Assert.That(loaded.Windows[1].AppState, Is.Null);
        }

        [Test]
        public void A_json_string_nested_past_the_codec_depth_is_rejected_before_load()
        {
            var builder = new StringBuilder();
            builder.Append("{\"Version\":1,\"Root\":");
            const int levels = 100;
            for (var level = 0; level < levels; level++)
            {
                builder.Append("{\"Children\":[");
            }

            builder.Append("{}");
            for (var level = 0; level < levels; level++)
            {
                builder.Append("]}");
            }

            builder.Append("}");
            var deeplyNestedJson = builder.ToString();

            Assert.That(
                () => JsonSerializer.Deserialize<SaveGame>(deeplyNestedJson),
                Throws.InstanceOf<JsonException>());
        }

        private static void Write(VirtualFileSystem vfs, string path, string text)
        {
            var stream = vfs.Open(path, OpenMode.Write, Root);
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
            stream.CloseWrite();
        }

        private static string Read(VirtualFileSystem vfs, string path)
        {
            var stream = vfs.Open(path, OpenMode.Read, Root);
            var buffer = new byte[256];
            var result = stream.Read(buffer, 0, buffer.Length);
            return Encoding.UTF8.GetString(buffer, 0, result.Count);
        }
    }
}
