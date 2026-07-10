using System;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// The UI window shell: clones the terminal template into a parent, surfaces the close
    /// button as an event and removes itself from the hierarchy. No kernel references.
    /// </summary>
    public sealed class TerminalWindow
    {
        public TerminalWindow(VisualElement parent, VisualTreeAsset template)
        {
            if (parent is null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (template is null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            Root = template.Instantiate();
            Root.style.flexGrow = 1;
            parent.Add(Root);
            var closeButton = Root.Q<Button>("close-button");
            closeButton.clicked += () => CloseRequested?.Invoke();
        }

        public VisualElement Root { get; }

        public event Action CloseRequested;

        public void Remove() => Root.RemoveFromHierarchy();
    }
}
