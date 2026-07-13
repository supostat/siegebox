namespace Siegebox.Shell
{
    /// <summary>
    /// The in-band control byte (RS, 0x1E) a secret writer prepends to a password prompt. The
    /// terminal output drain detects and strips it, then suppresses the echo of the line the
    /// player types in response. A C0 control char cannot be a const string, so it mirrors
    /// <see cref="ClearCommand"/>'s private-const-char plus public-static-readonly-string idiom.
    /// </summary>
    public static class SecretPromptMarker
    {
        private const char ControlByte = (char)0x1E;

        public static readonly string Sequence = ControlByte.ToString();
    }
}
