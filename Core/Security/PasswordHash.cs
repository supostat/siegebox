using System;
using System.Security.Cryptography;
using System.Text;

namespace Siegebox.Security
{
    /// <summary>
    /// Salted SHA-256 password hashing for the simulated user db. A stored hash is
    /// <c>saltHex$hashHex</c> where hash = SHA256(salt || utf8(password)). Not production
    /// key-stretching (this is a game), but never plaintext, so /etc/shadow is an honest
    /// gameplay artifact. <see cref="Verify"/> uses a length-constant comparison.
    /// </summary>
    public static class PasswordHash
    {
        private const int SaltBytes = 16;
        private const char Separator = '$';

        public static string Create(string password)
        {
            if (password is null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var salt = new byte[SaltBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            return ToHex(salt) + Separator + ToHex(Digest(salt, password));
        }

        public static bool Verify(string password, string stored)
        {
            if (password is null || stored is null)
            {
                return false;
            }

            var separator = stored.IndexOf(Separator);
            if (separator <= 0 || separator == stored.Length - 1)
            {
                return false;
            }

            if (!TryParseHex(stored.Substring(0, separator), out var salt))
            {
                return false;
            }

            return FixedTimeEquals(ToHex(Digest(salt, password)), stored.Substring(separator + 1));
        }

        private static byte[] Digest(byte[] salt, string password)
        {
            var passwordBytes = Encoding.UTF8.GetBytes(password);
            var input = new byte[salt.Length + passwordBytes.Length];
            Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
            Buffer.BlockCopy(passwordBytes, 0, input, salt.Length, passwordBytes.Length);
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(input);
            }
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new StringBuilder(bytes.Length * 2);
            foreach (var value in bytes)
            {
                builder.Append(value.ToString("x2"));
            }

            return builder.ToString();
        }

        private static bool TryParseHex(string hex, out byte[] bytes)
        {
            bytes = Array.Empty<byte>();
            if (hex.Length == 0 || hex.Length % 2 != 0)
            {
                return false;
            }

            var parsed = new byte[hex.Length / 2];
            for (var index = 0; index < parsed.Length; index++)
            {
                if (!byte.TryParse(hex.Substring(index * 2, 2), System.Globalization.NumberStyles.HexNumber, null, out parsed[index]))
                {
                    return false;
                }
            }

            bytes = parsed;
            return true;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var index = 0; index < left.Length; index++)
            {
                difference |= left[index] ^ right[index];
            }

            return difference == 0;
        }
    }
}
