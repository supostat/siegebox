namespace Siegebox.Persistence
{
    /// <summary>
    /// The persisted layout and identity of one open window: which app it hosts, where it sits,
    /// its display state and z-order, whether it held focus, and an opaque per-app state blob
    /// (null when the app does not persist state). Plain get/set for codec round-tripping.
    /// </summary>
    public sealed class WindowSnapshot
    {
        public string AppId { get; set; } = string.Empty;

        public float X { get; set; }

        public float Y { get; set; }

        public float Width { get; set; }

        public float Height { get; set; }

        public WindowDisplayState State { get; set; }

        public int ZOrderIndex { get; set; }

        public bool Focused { get; set; }

        public string? AppState { get; set; }
    }
}
