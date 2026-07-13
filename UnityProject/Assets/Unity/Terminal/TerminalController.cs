using System;
using Siegebox.Terminal;

namespace Siegebox.Unity
{
    /// <summary>
    /// Glue between the Core reader session and the view: forwards lines and history
    /// navigation, re-renders on scrollback version changes, and closes the session on
    /// dispose. No OS logic lives here.
    /// </summary>
    public sealed class TerminalController : IDisposable
    {
        private readonly TerminalSession session;
        private readonly TerminalView view;
        private readonly CommandHistory history = new CommandHistory();
        private int renderedScrollbackVersion = -1;

        public TerminalController(TerminalSession session, TerminalView view)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.view = view ?? throw new ArgumentNullException(nameof(view));

            view.LineSubmitted += OnLineSubmitted;
            view.HistoryPreviousRequested += OnHistoryPrevious;
            view.HistoryNextRequested += OnHistoryNext;
        }

        public void Pump()
        {
            session.Pump();
            if (session.ScrollbackVersion != renderedScrollbackVersion)
            {
                renderedScrollbackVersion = session.ScrollbackVersion;
                view.SetScrollback(session.ScrollbackText);
            }

            view.SetPrompt(session.IsBusy ? "" : session.PromptText);
            view.SetInputMasked(session.EchoSuppressed);
        }

        public void Dispose() => session.Close();

        private void OnLineSubmitted(string line)
        {
            var queued = session.SubmitLine(line);
            if (!session.LastSubmitWasSecret)
            {
                history.Add(line);
            }

            if (!queued)
            {
                view.SetInputText(line);
            }
        }

        private void OnHistoryPrevious()
        {
            if (history.TryMoveUp(out var line))
            {
                view.SetInputText(line);
            }
        }

        private void OnHistoryNext()
        {
            if (history.TryMoveDown(out var line))
            {
                view.SetInputText(line);
            }
        }
    }
}
