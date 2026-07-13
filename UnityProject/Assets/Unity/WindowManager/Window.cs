using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// One managed window: owns its frame geometry and state transitions, hosts an
    /// IWindowContent inside the chrome and surfaces user intents as events. Drag and
    /// resize apply only in the Normal state; the titlebar always stays reachable.
    /// </summary>
    public sealed class Window
    {
        public const float MinWidth = 240f;
        public const float MinHeight = 160f;

        private const float TitlebarReachableMargin = 40f;

        private readonly WindowChrome chrome;
        private Rect normalGeometry;
        private WindowState stateBeforeMinimize;

        public Window(VisualTreeAsset windowTemplate, IWindowContent content)
        {
            if (windowTemplate is null)
            {
                throw new ArgumentNullException(nameof(windowTemplate));
            }

            Content = content ?? throw new ArgumentNullException(nameof(content));

            Root = windowTemplate.Instantiate();
            Root.style.position = Position.Absolute;
            chrome = new WindowChrome(Root);
            chrome.SetTitle(content.Title);
            Root.Q<VisualElement>("window-content").Add(content.Root);
            WireChrome();
            Root.RegisterCallback<PointerDownEvent>(_ => FocusRequested?.Invoke(this), TrickleDown.TrickleDown);
        }

        public VisualElement Root { get; }

        public IWindowContent Content { get; }

        public WindowState State { get; private set; } = WindowState.Normal;

        /// <summary>
        /// The window's normal-state frame: its live rect when Normal, otherwise the rect
        /// captured before it was maximized or minimized. This is the geometry a save persists.
        /// </summary>
        public Rect Geometry => State == WindowState.Normal ? CurrentGeometry() : normalGeometry;

        public event Action<Window> CloseRequested;

        public event Action<Window> MinimizeRequested;

        public event Action<Window> MaximizeToggleRequested;

        public event Action<Window> FocusRequested;

        public void SetGeometry(Rect geometry)
        {
            Root.style.left = geometry.x;
            Root.style.top = geometry.y;
            Root.style.width = geometry.width;
            Root.style.height = geometry.height;
            normalGeometry = geometry;
        }

        public void SetFocused(bool focused)
        {
            chrome.SetFocused(focused);
            if (focused)
            {
                Content.OnFocusGained();
            }
            else
            {
                Content.OnFocusLost();
            }
        }

        public void Minimize()
        {
            if (State == WindowState.Minimized)
            {
                return;
            }

            if (State == WindowState.Normal)
            {
                normalGeometry = CurrentGeometry();
            }

            stateBeforeMinimize = State;
            State = WindowState.Minimized;
            Root.style.display = DisplayStyle.None;
        }

        public void RestoreFromMinimized()
        {
            if (State != WindowState.Minimized)
            {
                return;
            }

            Root.style.display = DisplayStyle.Flex;
            State = stateBeforeMinimize;
        }

        public void ToggleMaximize()
        {
            if (State == WindowState.Maximized)
            {
                Root.style.right = StyleKeyword.Auto;
                Root.style.bottom = StyleKeyword.Auto;
                State = WindowState.Normal;
                SetGeometry(normalGeometry);
                chrome.SetMaximized(false);
                return;
            }

            normalGeometry = CurrentGeometry();
            Root.style.left = 0f;
            Root.style.top = 0f;
            Root.style.right = 0f;
            Root.style.bottom = 0f;
            Root.style.width = StyleKeyword.Auto;
            Root.style.height = StyleKeyword.Auto;
            State = WindowState.Maximized;
            chrome.SetMaximized(true);
        }

        public void MoveBy(Vector2 delta)
        {
            if (State != WindowState.Normal)
            {
                return;
            }

            var left = Root.resolvedStyle.left + delta.x;
            var top = Root.resolvedStyle.top + delta.y;
            var parentSize = Root.parent.layout.size;
            if (!float.IsNaN(parentSize.x) && !float.IsNaN(parentSize.y))
            {
                left = Mathf.Clamp(left, TitlebarReachableMargin - Root.resolvedStyle.width, parentSize.x - TitlebarReachableMargin);
                top = Mathf.Clamp(top, 0f, parentSize.y - TitlebarReachableMargin);
            }

            Root.style.left = left;
            Root.style.top = top;
        }

        public void ResizeBy(Vector2 delta)
        {
            if (State != WindowState.Normal)
            {
                return;
            }

            Root.style.width = Mathf.Max(MinWidth, Root.resolvedStyle.width + delta.x);
            Root.style.height = Mathf.Max(MinHeight, Root.resolvedStyle.height + delta.y);
        }

        private Rect CurrentGeometry()
        {
            var layout = Root.layout;
            if (float.IsNaN(layout.width) || float.IsNaN(layout.height))
            {
                return normalGeometry;
            }

            return layout;
        }

        private void WireChrome()
        {
            chrome.CloseClicked += () => CloseRequested?.Invoke(this);
            chrome.MinimizeClicked += () => MinimizeRequested?.Invoke(this);
            chrome.MaximizeClicked += () => MaximizeToggleRequested?.Invoke(this);
            chrome.TitleDragged += MoveBy;
            chrome.ResizeDragged += ResizeBy;
        }
    }
}
