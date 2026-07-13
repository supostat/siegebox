using System;
using Siegebox.App;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Hosts a Core-only text app in a window: renders its Text into a Label with rich
    /// text disabled (mods control the string) and polls the surface's Revision from
    /// Pump. Focus and close notifications forward to the app's lifecycle hooks.
    /// </summary>
    public sealed class TextAppContent : IWindowContent
    {
        private readonly IApp app;
        private readonly ITextContentApp surface;
        private readonly Label textLabel;
        private int renderedRevision;

        public TextAppContent(IApp app, ITextContentApp surface)
        {
            this.app = app ?? throw new ArgumentNullException(nameof(app));
            this.surface = surface ?? throw new ArgumentNullException(nameof(surface));
            var scrollView = new ScrollView();
            scrollView.AddToClassList("text-app-content");
            scrollView.style.flexGrow = 1;
            textLabel = new Label(surface.Text);
            textLabel.enableRichText = false;
            textLabel.AddToClassList("text-app-message");
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            scrollView.Add(textLabel);
            Root = scrollView;
            renderedRevision = surface.Revision;
        }

        public string Title => surface.Title;

        public WindowIdentity Identity => WindowIdentity.None;

        public VisualElement Root { get; }

        public void Pump()
        {
            if (renderedRevision == surface.Revision)
            {
                return;
            }

            renderedRevision = surface.Revision;
            textLabel.text = surface.Text;
        }

        public void OnFocusGained() => app.OnFocusGained();

        public void OnFocusLost() => app.OnFocusLost();

        public void OnClosed() => app.OnClosed();
    }
}
