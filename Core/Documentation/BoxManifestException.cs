using System;

namespace Siegebox.Documentation
{
    /// <summary>Raised when the box manifest cannot be parsed or fails validation.</summary>
    public sealed class BoxManifestException : Exception
    {
        public BoxManifestException(string message)
            : base(message)
        {
        }
    }
}
