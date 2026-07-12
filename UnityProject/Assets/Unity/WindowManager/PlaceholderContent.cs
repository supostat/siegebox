using System;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Minimal non-terminal window content built in code: a centered message. Proves the
    /// window manager hosts arbitrary content, not just the terminal.
    /// </summary>
    public sealed class PlaceholderContent : IWindowContent
    {
        public PlaceholderContent(string title, string message)
        {
            if (title is null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (message is null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            Title = title;
            var root = new VisualElement();
            root.AddToClassList("placeholder-content");
            root.style.flexGrow = 1;
            var messageLabel = new Label(message);
            messageLabel.AddToClassList("placeholder-message");
            root.Add(messageLabel);
            Root = root;
        }

        public string Title { get; }

        public VisualElement Root { get; }

        public void Pump()
        {
        }

        public void OnFocusGained()
        {
        }

        public void OnFocusLost()
        {
        }

        public void OnClosed()
        {
        }
    }
}
