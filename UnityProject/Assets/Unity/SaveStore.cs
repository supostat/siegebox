using System;
using System.IO;
using Newtonsoft.Json;
using Siegebox.Persistence;
using UnityEngine;

namespace Siegebox.Unity
{
    /// <summary>
    /// The only Unity-resident bytes/JSON seam: a Newtonsoft codec plus the save-file IO. The
    /// codec's MaxDepth is the real untrusted-input DoS boundary — it caps nesting while parsing
    /// the file, before Core ever sees a tree. Every read wraps the codec call so a malformed
    /// file (including a depth overflow) surfaces as a handled failure — a false from
    /// <see cref="TryRead"/> or a <see cref="SaveFormatException"/> from <see cref="Deserialize{T}"/> —
    /// never a raw library exception. Core never references Newtonsoft.
    /// </summary>
    public static class SaveStore
    {
        private const int MaxDepth = 64;
        private const string SaveFileName = "siegebox.save.json";

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MaxDepth = MaxDepth,
            Formatting = Formatting.None,
            TypeNameHandling = TypeNameHandling.None
        };

        public static string Serialize<T>(T value) => JsonConvert.SerializeObject(value, Settings);

        public static T Deserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json, Settings);
            }
            catch (JsonException error)
            {
                throw new SaveFormatException("The save data is malformed or too deeply nested.", error);
            }
        }

        public static void Write(SaveGame save)
        {
            if (save is null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            File.WriteAllText(SaveFilePath, JsonConvert.SerializeObject(save, Settings));
        }

        public static bool TryRead(out SaveGame save)
        {
            save = null;
            if (!File.Exists(SaveFilePath))
            {
                return false;
            }

            try
            {
                save = JsonConvert.DeserializeObject<SaveGame>(File.ReadAllText(SaveFilePath), Settings);
                return save != null;
            }
            catch (JsonException)
            {
                return false;
            }
            catch (IOException)
            {
                return false;
            }
        }

        private static string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);
    }
}
