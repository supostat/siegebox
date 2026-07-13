using System;

namespace Siegebox.Unity
{
    /// <summary>
    /// The presentation-only identity a window carries: user name, uid and whether the
    /// session is privileged. Primitives only — no kernel type crosses the windowing
    /// layer; the composition root converts Credentials into this. The default value
    /// (None) has a null UserName and therefore no user, which windows treat as "unowned".
    /// </summary>
    public readonly struct WindowIdentity
    {
        public static readonly WindowIdentity None = default;

        public WindowIdentity(string userName, int uid, bool isPrivileged)
        {
            UserName = userName ?? throw new ArgumentNullException(nameof(userName));
            Uid = uid;
            IsPrivileged = isPrivileged;
        }

        public string UserName { get; }

        public int Uid { get; }

        public bool IsPrivileged { get; }

        public bool HasUser => UserName != null;

        public string PromptGlyph => IsPrivileged ? "#" : "$";

        public string ChromeLabel => $"{PromptGlyph} {UserName} ({Uid})";
    }
}
