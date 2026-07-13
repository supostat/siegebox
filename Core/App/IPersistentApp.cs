namespace Siegebox.App
{
    /// <summary>
    /// Opt-in persistence for an app: the host captures an opaque state string when saving and
    /// hands the same string back after relaunching the app on load. The string is app-private;
    /// the host never inspects it.
    /// </summary>
    public interface IPersistentApp
    {
        string CaptureState();

        void RestoreState(string state);
    }
}
