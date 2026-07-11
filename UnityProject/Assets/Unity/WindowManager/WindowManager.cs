using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Owns every open window: creation with cascade placement, z-order (child order in
    /// the window layer), the single-focus rule and the close path that notifies content.
    /// Knows windows only through IWindowContent — never a concrete content type.
    /// </summary>
    public sealed class WindowManager
    {
        private const int CascadeSlots = 8;

        private static readonly Vector2 CascadeOrigin = new Vector2(48f, 48f);
        private static readonly Vector2 CascadeStep = new Vector2(24f, 24f);
        private static readonly Vector2 DefaultSize = new Vector2(640f, 420f);

        private readonly VisualElement windowLayer;
        private readonly VisualTreeAsset windowTemplate;
        private readonly List<Window> windows = new List<Window>();
        private int openedCount;

        public WindowManager(VisualElement windowLayer, VisualTreeAsset windowTemplate)
        {
            this.windowLayer = windowLayer ?? throw new ArgumentNullException(nameof(windowLayer));
            this.windowTemplate = windowTemplate ?? throw new ArgumentNullException(nameof(windowTemplate));
        }

        public IReadOnlyList<Window> Windows => windows;

        public Window FocusedWindow { get; private set; }

        public event Action<Window> WindowOpened;

        public event Action<Window> WindowClosed;

        public event Action<Window> WindowStateChanged;

        public event Action<Window> WindowFocused;

        public Window Open(IWindowContent content)
        {
            if (content is null)
            {
                throw new ArgumentNullException(nameof(content));
            }

            var window = new Window(windowTemplate, content);
            window.CloseRequested += Close;
            window.FocusRequested += Focus;
            window.MinimizeRequested += Minimize;
            window.MaximizeToggleRequested += ToggleMaximize;

            windows.Add(window);
            windowLayer.Add(window.Root);
            var origin = CascadeOrigin + CascadeStep * (openedCount % CascadeSlots);
            openedCount++;
            window.SetGeometry(new Rect(origin.x, origin.y, DefaultSize.x, DefaultSize.y));
            WindowOpened?.Invoke(window);
            Focus(window);
            return window;
        }

        public void Close(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (!windows.Remove(window))
            {
                return;
            }

            window.Root.RemoveFromHierarchy();
            window.Content.OnClosed();
            WindowClosed?.Invoke(window);
            if (FocusedWindow == window)
            {
                FocusedWindow = null;
                FocusTopmostVisible();
            }
        }

        public void Focus(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (window.State == WindowState.Minimized)
            {
                return;
            }

            window.Root.BringToFront();
            if (FocusedWindow == window)
            {
                return;
            }

            FocusedWindow?.SetFocused(false);
            FocusedWindow = window;
            window.SetFocused(true);
            WindowFocused?.Invoke(window);
        }

        public void Minimize(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (window.State == WindowState.Minimized)
            {
                return;
            }

            window.Minimize();
            WindowStateChanged?.Invoke(window);
            if (FocusedWindow == window)
            {
                window.SetFocused(false);
                FocusedWindow = null;
                FocusTopmostVisible();
            }
        }

        public void Restore(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (window.State != WindowState.Minimized)
            {
                return;
            }

            window.RestoreFromMinimized();
            WindowStateChanged?.Invoke(window);
            Focus(window);
        }

        public void ToggleMaximize(Window window)
        {
            if (window is null)
            {
                throw new ArgumentNullException(nameof(window));
            }

            if (window.State == WindowState.Minimized)
            {
                return;
            }

            window.ToggleMaximize();
            WindowStateChanged?.Invoke(window);
            Focus(window);
        }

        public void CloseAll()
        {
            foreach (var window in windows.ToArray())
            {
                Close(window);
            }
        }

        private void FocusTopmostVisible()
        {
            for (var childIndex = windowLayer.childCount - 1; childIndex >= 0; childIndex--)
            {
                var child = windowLayer[childIndex];
                var window = windows.Find(candidate => candidate.Root == child);
                if (window != null && window.State != WindowState.Minimized)
                {
                    Focus(window);
                    return;
                }
            }

            FocusedWindow = null;
            WindowFocused?.Invoke(null);
        }
    }
}
