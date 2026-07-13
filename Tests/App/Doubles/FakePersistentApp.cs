namespace Siegebox.App.Tests
{
    /// <summary>Minimal IApp that also persists an opaque state string via IPersistentApp.</summary>
    internal sealed class FakePersistentApp : IApp, IPersistentApp
    {
        public AppState State { get; private set; } = AppState.Created;

        public string PersistedState { get; private set; } = string.Empty;

        public void OnLaunched() => State = AppState.Running;

        public void OnFocusGained()
        {
        }

        public void OnFocusLost()
        {
        }

        public void OnClosed() => State = AppState.Closed;

        public string CaptureState() => PersistedState;

        public void RestoreState(string state) => PersistedState = state;
    }
}
