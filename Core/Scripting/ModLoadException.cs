using System;

namespace Siegebox.Scripting
{
    /// <summary>Raised when a mod manifest or one of its scripts cannot be loaded.</summary>
    public sealed class ModLoadException : Exception
    {
        public ModLoadException(string message)
            : base(message)
        {
        }
    }
}
