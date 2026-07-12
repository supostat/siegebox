using System;

namespace Siegebox.Scripting
{
    /// <summary>Outcome of loading one mod: Error is empty exactly when the mod loaded.</summary>
    public sealed class ModLoadResult
    {
        public ModLoadResult(string modId, bool loaded, string error)
        {
            if (modId is null)
            {
                throw new ArgumentNullException(nameof(modId));
            }

            if (string.IsNullOrWhiteSpace(modId))
            {
                throw new ArgumentException("A mod id must not be blank.", nameof(modId));
            }

            if (error is null)
            {
                throw new ArgumentNullException(nameof(error));
            }

            if (loaded && error.Length != 0)
            {
                throw new ArgumentException("A loaded result must not carry an error.", nameof(error));
            }

            if (!loaded && error.Length == 0)
            {
                throw new ArgumentException("A failed result must carry an error.", nameof(error));
            }

            ModId = modId;
            Loaded = loaded;
            Error = error;
        }

        public string ModId { get; }

        public bool Loaded { get; }

        public string Error { get; }
    }
}
