namespace Siegebox.App.Tests
{
    /// <summary>Minimal IApp: tracks lifecycle state and nothing else.</summary>
    internal sealed class FakeApp : IApp
    {
        public AppState State { get; private set; } = AppState.Created;

        public void OnLaunched() => State = AppState.Running;

        public void OnFocusGained()
        {
        }

        public void OnFocusLost()
        {
        }

        public void OnClosed() => State = AppState.Closed;
    }
}
