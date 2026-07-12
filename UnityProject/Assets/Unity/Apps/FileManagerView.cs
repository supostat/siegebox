using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Plain view over the file manager UXML: renders the current path, the entry rows and
    /// an error status line; forwards directory activation and up-navigation as events.
    /// </summary>
    public sealed class FileManagerView
    {
        private readonly Label pathLabel;
        private readonly Label statusLabel;
        private readonly VisualElement entryList;

        public FileManagerView(VisualElement root)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            pathLabel = root.Q<Label>("path-label");
            statusLabel = root.Q<Label>("status-label");
            entryList = root.Q<VisualElement>("entry-list");
            var upButton = root.Q<Button>("up-button");
            pathLabel.enableRichText = false;
            statusLabel.enableRichText = false;
            upButton.clicked += () => UpRequested?.Invoke();
        }

        public event Action<string> EntryActivated;

        public event Action UpRequested;

        public void SetPath(string path) => pathLabel.text = path;

        public void SetStatus(string message)
        {
            statusLabel.text = message;
            statusLabel.style.display = DisplayStyle.Flex;
        }

        public void ClearStatus()
        {
            statusLabel.text = "";
            statusLabel.style.display = DisplayStyle.None;
        }

        public void SetEntries(IReadOnlyList<FileManagerEntry> entries)
        {
            entryList.Clear();
            foreach (var entry in entries)
            {
                entryList.Add(entry.IsDirectory ? DirectoryRow(entry) : FileRow(entry));
            }
        }

        private VisualElement DirectoryRow(FileManagerEntry entry)
        {
            var row = new Button(() => EntryActivated?.Invoke(entry.Name)) { text = entry.Name + "/" };
            row.enableRichText = false;
            row.AddToClassList("entry-row");
            row.AddToClassList("entry-row--directory");
            return row;
        }

        private static VisualElement FileRow(FileManagerEntry entry)
        {
            var row = new VisualElement();
            row.AddToClassList("entry-row");
            var nameLabel = new Label(entry.Name);
            nameLabel.enableRichText = false;
            nameLabel.style.flexGrow = 1;
            var sizeLabel = new Label(entry.Size + " B");
            sizeLabel.enableRichText = false;
            sizeLabel.AddToClassList("entry-size");
            row.Add(nameLabel);
            row.Add(sizeLabel);
            return row;
        }
    }
}
