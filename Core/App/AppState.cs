namespace Siegebox.App
{
    /// <summary>
    /// Lifecycle of one app instance: built by its factory, running after the host
    /// launches it, closed together with its hosting window.
    /// </summary>
    public enum AppState
    {
        Created,
        Running,
        Closed
    }
}
