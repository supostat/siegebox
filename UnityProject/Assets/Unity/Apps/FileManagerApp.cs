using System;
using System.Collections.Generic;
using Siegebox.App;
using Siegebox.Vfs;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Read-only browser over the virtual file system: shows one directory at a time,
    /// descends into subdirectories, walks up with "..", and surfaces VFS errors in a
    /// status line. Refreshes on navigation and on window refocus.
    /// </summary>
    public sealed class FileManagerApp : IApp, IWindowContent, IPersistentApp
    {
        private readonly VirtualFileSystem vfs;
        private readonly Credentials credentials;
        private readonly FileManagerView view;
        private string currentPath = "/";

        public FileManagerApp(VisualTreeAsset template, VirtualFileSystem vfs, Credentials credentials)
        {
            if (template is null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            this.credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));

            Root = template.Instantiate();
            Root.style.flexGrow = 1;
            view = new FileManagerView(Root);
            view.EntryActivated += EnterDirectory;
            view.UpRequested += NavigateUp;
            ShowDirectory("/");
        }

        public string Title => "files";

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

        public void OnFocusGained() => ShowDirectory(currentPath);

        public void OnFocusLost()
        {
        }

        public void OnClosed() => State = AppState.Closed;

        public string CaptureState() => currentPath;

        public void RestoreState(string state) => currentPath = string.IsNullOrEmpty(state) ? "/" : state;

        private void EnterDirectory(string name) => ShowDirectory(ChildPath(currentPath, name));

        private void NavigateUp() => ShowDirectory(currentPath + "/..");

        private void ShowDirectory(string path)
        {
            try
            {
                var canonicalPath = vfs.ResolveDirectoryPath(path, credentials);
                var entries = ReadEntries(canonicalPath);
                currentPath = canonicalPath;
                view.SetPath(canonicalPath);
                view.SetEntries(entries);
                view.ClearStatus();
            }
            catch (VfsException error)
            {
                view.SetStatus($"{error.Path}: {error.Error}");
            }
        }

        private IReadOnlyList<FileManagerEntry> ReadEntries(string directoryPath)
        {
            var names = vfs.List(directoryPath, credentials);
            var entries = new List<FileManagerEntry>(names.Count);
            foreach (var name in names)
            {
                var info = vfs.Stat(ChildPath(directoryPath, name), credentials);
                entries.Add(new FileManagerEntry(name, info.Type == NodeType.Directory, info.Size));
            }

            return entries;
        }

        private static string ChildPath(string directoryPath, string name)
            => directoryPath == "/" ? "/" + name : directoryPath + "/" + name;
    }
}
