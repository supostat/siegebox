namespace Siegebox.Persistence
{
    /// <summary>
    /// The save schema version. <see cref="EnsureSupported"/> is the single gate the loader
    /// runs before it trusts any persisted tree, so an unknown version aborts the load with a
    /// <see cref="SaveFormatException"/> that names only the version.
    /// </summary>
    /// <remarks>
    /// v1 is the first format, so there are no older versions to migrate yet. When a v2 ships,
    /// widen <see cref="EnsureSupported"/> to accept the known-older range and add the upgrade
    /// step in <c>SaveSerializer.Load</c> between the version check and the tree import.
    /// </remarks>
    public static class SaveVersion
    {
        public const int Current = 1;

        public static void EnsureSupported(int version)
        {
            if (version != Current)
            {
                throw new SaveFormatException($"Unsupported save version {version}; expected {Current}.");
            }
        }
    }
}
