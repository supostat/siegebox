using System;
using Siegebox.Terminal;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// The terminal as window content: composes the view and controller over one reader
    /// session. Window focus drives input focus; closing the window closes the session,
    /// which runs the hangup cascade.
    /// </summary>
    public sealed class TerminalContent : IWindowContent
    {
        private readonly TerminalView view;
        private readonly TerminalController controller;

        public TerminalContent(VisualTreeAsset terminalTemplate, TerminalSession session)
        {
            if (terminalTemplate is null)
            {
                throw new ArgumentNullException(nameof(terminalTemplate));
            }

            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Root = terminalTemplate.Instantiate();
            Root.style.flexGrow = 1;
            view = new TerminalView(Root);
            controller = new TerminalController(session, view);
        }

        public string Title => "terminal";

        public VisualElement Root { get; }

        public void Pump() => controller.Pump();

        public void OnFocusGained() => view.FocusInput();

        public void OnFocusLost() => view.BlurInput();

        public void OnClosed() => controller.Dispose();
    }
}
