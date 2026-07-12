namespace Siegebox.App
{
    /// <summary>
    /// Uniform app lifecycle with no engine types. The host calls OnLaunched exactly once
    /// before showing the app; the focus and close hooks mirror the hosting window's
    /// notifications.
    /// </summary>
    public interface IApp
    {
        AppState State { get; }

        void OnLaunched();

        void OnFocusGained();

        void OnFocusLost();

        /// <summary>The host calls this at most once, when the hosting window closes.</summary>
        void OnClosed();
    }
}
