using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Launchers on the left, one entry per open window on the right. Entry clicks cycle
    /// minimized → restore, focused → minimize, otherwise focus; highlights follow the
    /// manager's focus and state events.
    /// </summary>
    public sealed class Taskbar
    {
        private const string EntryFocusedClassName = "taskbar-entry--focused";
        private const string EntryMinimizedClassName = "taskbar-entry--minimized";

        private readonly VisualElement launchersRoot;
        private readonly VisualElement entriesRoot;
        private readonly WindowManager windowManager;
        private readonly Dictionary<Window, Button> entries = new Dictionary<Window, Button>();

        public Taskbar(VisualElement root, WindowManager windowManager)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            this.windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));

            launchersRoot = root.Q<VisualElement>("taskbar-launchers");
            entriesRoot = root.Q<VisualElement>("taskbar-entries");

            windowManager.WindowOpened += AddEntry;
            windowManager.WindowClosed += RemoveEntry;
            windowManager.WindowFocused += HighlightFocused;
            windowManager.WindowStateChanged += RefreshEntry;
        }

        public void AddLauncher(string label, Action launch)
        {
            if (label is null)
            {
                throw new ArgumentNullException(nameof(label));
            }

            if (launch is null)
            {
                throw new ArgumentNullException(nameof(launch));
            }

            var button = new Button(launch) { text = label };
            button.AddToClassList("taskbar-launcher");
            launchersRoot.Add(button);
        }

        private void AddEntry(Window window)
        {
            var button = new Button(() => OnEntryClicked(window)) { text = window.Content.Title };
            button.AddToClassList("taskbar-entry");
            entries[window] = button;
            entriesRoot.Add(button);
        }

        private void RemoveEntry(Window window)
        {
            if (!entries.TryGetValue(window, out var button))
            {
                return;
            }

            entries.Remove(window);
            button.RemoveFromHierarchy();
        }

        private void OnEntryClicked(Window window)
        {
            if (window.State == WindowState.Minimized)
            {
                windowManager.Restore(window);
                return;
            }

            if (windowManager.FocusedWindow == window)
            {
                windowManager.Minimize(window);
                return;
            }

            windowManager.Focus(window);
        }

        private void HighlightFocused(Window focused)
        {
            foreach (var entry in entries)
            {
                entry.Value.EnableInClassList(EntryFocusedClassName, entry.Key == focused);
            }
        }

        private void RefreshEntry(Window window)
        {
            if (entries.TryGetValue(window, out var button))
            {
                button.EnableInClassList(EntryMinimizedClassName, window.State == WindowState.Minimized);
            }
        }
    }
}
