using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// What a window hosts: a titled visual tree plus focus and close notifications.
    /// UI-only — the window manager never sees kernel types through this contract.
    /// </summary>
    public interface IWindowContent
    {
        string Title { get; }

        VisualElement Root { get; }

        void OnFocusGained();

        void OnFocusLost();

        void OnClosed();
    }
}
