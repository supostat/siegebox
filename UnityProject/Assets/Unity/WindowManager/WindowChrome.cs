using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Dumb frame UI over the instantiated window template: surfaces titlebar buttons,
    /// title dragging and resize-handle dragging as events. No geometry math, no kernel
    /// references.
    /// </summary>
    public sealed class WindowChrome
    {
        private const string FocusedClassName = "window--focused";

        private const string RestoreClassName = "title-button--restore";

        private readonly VisualElement frame;
        private readonly Label windowTitle;
        private readonly Label identityIndicator;
        private readonly Button maximizeButton;

        public WindowChrome(VisualElement root)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            frame = root.Q<VisualElement>("window");
            windowTitle = root.Q<Label>("window-title");
            windowTitle.enableRichText = false;
            identityIndicator = root.Q<Label>("identity-indicator");
            identityIndicator.enableRichText = false;
            var minimizeButton = root.Q<Button>("minimize-button");
            maximizeButton = root.Q<Button>("maximize-button");
            var closeButton = root.Q<Button>("close-button");
            var dragRegion = root.Q<VisualElement>("drag-region");
            var resizeRight = root.Q<VisualElement>("resize-handle-right");
            var resizeBottom = root.Q<VisualElement>("resize-handle-bottom");
            var resizeCorner = root.Q<VisualElement>("resize-handle-corner");

            minimizeButton.clicked += () => MinimizeClicked?.Invoke();
            maximizeButton.clicked += () => MaximizeClicked?.Invoke();
            closeButton.clicked += () => CloseClicked?.Invoke();
            dragRegion.AddManipulator(new PointerDragManipulator(delta => TitleDragged?.Invoke(delta)));
            resizeRight.AddManipulator(new PointerDragManipulator(delta => ResizeDragged?.Invoke(new Vector2(delta.x, 0f))));
            resizeBottom.AddManipulator(new PointerDragManipulator(delta => ResizeDragged?.Invoke(new Vector2(0f, delta.y))));
            resizeCorner.AddManipulator(new PointerDragManipulator(delta => ResizeDragged?.Invoke(delta)));
        }

        public event Action MinimizeClicked;

        public event Action MaximizeClicked;

        public event Action CloseClicked;

        public event Action<Vector2> TitleDragged;

        public event Action<Vector2> ResizeDragged;

        public void SetTitle(string title) => windowTitle.text = title;

        public void SetIdentity(WindowIdentity identity)
        {
            if (identity.HasUser)
            {
                identityIndicator.text = identity.ChromeLabel;
                identityIndicator.style.display = DisplayStyle.Flex;
                return;
            }

            identityIndicator.text = string.Empty;
            identityIndicator.style.display = DisplayStyle.None;
        }

        public void SetFocused(bool focused) => frame.EnableInClassList(FocusedClassName, focused);

        public void SetMaximized(bool maximized) => maximizeButton.EnableInClassList(RestoreClassName, maximized);
    }
}
