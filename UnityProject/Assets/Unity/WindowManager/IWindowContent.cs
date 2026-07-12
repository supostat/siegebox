using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// What a window hosts: a titled visual tree, a per-frame Pump hook, and focus and
    /// close notifications. UI-only — the window manager never sees kernel types through
    /// this contract.
    /// </summary>
    public interface IWindowContent
    {
        string Title { get; }

        VisualElement Root { get; }

        /// <summary>
        /// Per-frame update hook; the host calls this every frame while the window is open.
        /// Implementations must not open or close windows from Pump — the host iterates
        /// the live window list.
        /// </summary>
        void Pump();

        void OnFocusGained();

        void OnFocusLost();

        void OnClosed();
    }
}
