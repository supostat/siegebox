using System;

namespace Siegebox.Security
{
    /// <summary>Thrown when /etc/passwd or /etc/shadow content is malformed.</summary>
    public sealed class UserDatabaseException : Exception
    {
        public UserDatabaseException(string message)
            : base(message)
        {
        }
    }
}
