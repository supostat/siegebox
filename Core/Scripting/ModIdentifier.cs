using System.Text.RegularExpressions;

namespace Siegebox.Scripting
{
    /// <summary>
    /// The one identifier allowlist shared by manifest ids, dependencies and script-facing
    /// registration names: a lowercase letter, then up to 63 lowercase letters, digits,
    /// '_' or '-'.
    /// </summary>
    internal static class ModIdentifier
    {
        public const string Rule = "^[a-z][a-z0-9_-]{0,63}$";

        private static readonly Regex Pattern = new Regex(Rule);

        public static bool IsValid(string identifier) => identifier != null && Pattern.IsMatch(identifier);
    }
}
