using System;
using System.Collections.Generic;
using Siegebox.App;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// The doc-browser window: a left navigation of categories and command names, a center viewer
    /// that shows the selected manual page, and a hints block fed by the per-box manifest. Every
    /// label rendering VFS or OS text has rich text disabled so authored content is shown verbatim.
    /// The hints block re-reads on refocus and hides itself when there are no hints.
    /// </summary>
    public sealed class DocBrowserContent : IApp, IWindowContent
    {
        private readonly DocBrowser browser;
        private readonly Label viewer;
        private readonly VisualElement hints;

        public DocBrowserContent(DocBrowser browser, WindowIdentity identity)
        {
            this.browser = browser ?? throw new ArgumentNullException(nameof(browser));
            Identity = identity;

            Root = new VisualElement();
            Root.style.flexGrow = 1;
            Root.style.flexDirection = FlexDirection.Column;

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Row;

            viewer = new Label { enableRichText = false };
            viewer.style.flexGrow = 1;
            var viewerScroll = new ScrollView();
            viewerScroll.style.flexGrow = 1;
            viewerScroll.Add(viewer);

            body.Add(BuildNavigation());
            body.Add(viewerScroll);

            hints = new VisualElement();
            Root.Add(body);
            Root.Add(hints);
            RefreshHints();
        }

        public string Title => "docs";

        public WindowIdentity Identity { get; }

        public VisualElement Root { get; }

        public AppState State { get; private set; } = AppState.Created;

        public void OnLaunched()
        {
            if (State != AppState.Created)
            {
                throw new InvalidOperationException("An app instance launches once.");
            }

            State = AppState.Running;
        }

        public void Pump()
        {
        }

        public void OnFocusGained() => RefreshHints();

        public void OnFocusLost()
        {
        }

        public void OnClosed() => State = AppState.Closed;

        private VisualElement BuildNavigation()
        {
            var navigation = new ScrollView();
            navigation.style.width = 200;
            foreach (var category in browser.Categories)
            {
                var header = new Label(category.Name) { enableRichText = false };
                header.style.unityFontStyleAndWeight = FontStyle.Bold;
                navigation.Add(header);
                foreach (var entry in category.Entries)
                {
                    navigation.Add(CommandButton(entry));
                }
            }

            return navigation;
        }

        private Button CommandButton(DocEntry entry)
        {
            var commandName = entry.CommandName;
            var label = entry.Description.Length == 0
                ? commandName
                : $"{commandName}  —  {entry.Description}";
            var button = new Button(() => ShowPage(commandName)) { text = label };
            button.enableRichText = false;
            return button;
        }

        private void ShowPage(string commandName)
        {
            try
            {
                viewer.text = browser.ReadPage(commandName);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                viewer.text = "could not load page";
            }
        }

        private void RefreshHints()
        {
            hints.Clear();

            IReadOnlyList<string> lines;
            try
            {
                lines = browser.ReadHints();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                hints.style.display = DisplayStyle.None;
                return;
            }

            if (lines.Count == 0)
            {
                hints.style.display = DisplayStyle.None;
                return;
            }

            hints.style.display = DisplayStyle.Flex;
            foreach (var line in lines)
            {
                hints.Add(new Label(line) { enableRichText = false });
            }
        }
    }
}
