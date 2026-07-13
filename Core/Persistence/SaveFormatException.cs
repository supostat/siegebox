using System;

namespace Siegebox.Persistence
{
    /// <summary>
    /// A handled load failure: the save is malformed, an unsupported version, or structurally
    /// invalid at the boundary the serializer checks. Messages reference structural position,
    /// node type or version ONLY — never node content bytes, so a persisted password hash can
    /// never leak into an error surface.
    /// </summary>
    public sealed class SaveFormatException : Exception
    {
        public SaveFormatException(string message)
            : base(message)
        {
        }

        public SaveFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
