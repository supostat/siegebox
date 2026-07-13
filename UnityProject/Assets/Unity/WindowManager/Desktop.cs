using System;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// The desktop shell: clones the desktop template into a parent and exposes the two
    /// regions everything else builds on — the window layer and the taskbar root.
    /// </summary>
    public sealed class Desktop
    {
        public Desktop(VisualElement parent, VisualTreeAsset desktopTemplate)
        {
            if (parent is null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (desktopTemplate is null)
            {
                throw new ArgumentNullException(nameof(desktopTemplate));
            }

            var root = desktopTemplate.Instantiate();
            root.style.flexGrow = 1;
            parent.Add(root);
            WindowLayer = root.Q<VisualElement>("window-layer");
            TaskbarRoot = root.Q<VisualElement>("taskbar");
            SystemPanelRoot = root.Q<VisualElement>("system-panel");
        }

        public VisualElement WindowLayer { get; }

        public VisualElement TaskbarRoot { get; }

        public VisualElement SystemPanelRoot { get; }
    }
}
