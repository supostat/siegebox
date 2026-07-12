using System;

namespace Siegebox.App
{
    /// <summary>Registry-launchable app showing one fixed text: the body never changes after construction.</summary>
    public sealed class StaticTextApp : IApp, ITextContentApp
    {
        public StaticTextApp(string title, string text)
        {
            if (title is null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("A title must not be blank.", nameof(title));
            }

            if (text is null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            Title = title;
            Text = text;
        }

        public string Title { get; }

        public string Text { get; }

        public int Revision => 0;

        public AppState State { get; private set; } = AppState.Created;

        public void OnLaunched()
        {
            if (State != AppState.Created)
            {
                throw new InvalidOperationException("An app instance launches once.");
            }

            State = AppState.Running;
        }

        public void OnFocusGained()
        {
        }

        public void OnFocusLost()
        {
        }

        public void OnClosed() => State = AppState.Closed;
    }
}
