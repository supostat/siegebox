namespace Siegebox.App
{
    /// <summary>
    /// Launch request boundary: Core resolves the descriptor, the host materializes the app.
    /// </summary>
    public interface IAppLauncher
    {
        void Launch(AppDescriptor descriptor);
    }
}
