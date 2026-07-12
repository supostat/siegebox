using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// Plain view over the terminal UXML: renders scrollback and prompt text, forwards
    /// submitted lines and history navigation keys. Auto-scroll stays pinned to the bottom
    /// until the user scrolls up, and re-pins when they scroll back down.
    /// </summary>
    public sealed class TerminalView
    {
        private const float PinThreshold = 1f;

        private readonly ScrollView outputScroll;
        private readonly Label outputText;
        private readonly Label prompt;
        private readonly TextField commandInput;
        private bool pinnedToBottom = true;

        public TerminalView(VisualElement root)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            outputScroll = root.Q<ScrollView>("output-scroll");
            outputText = root.Q<Label>("output-text");
            prompt = root.Q<Label>("prompt");
            commandInput = root.Q<TextField>("command-input");
            outputText.enableRichText = false;
            prompt.enableRichText = false;

            commandInput.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            outputScroll.verticalScroller.valueChanged += OnScrolled;
            outputText.RegisterCallback<GeometryChangedEvent>(OnOutputGeometryChanged);
        }

        public event Action<string> LineSubmitted;

        public event Action HistoryPreviousRequested;

        public event Action HistoryNextRequested;

        public void SetScrollback(string text) => outputText.text = text;

        public void SetPrompt(string text) => prompt.text = text;

        public void SetInputText(string text)
        {
            commandInput.value = text;
            commandInput.SelectRange(text.Length, text.Length);
        }

        public void FocusInput()
            => commandInput.schedule.Execute(() => commandInput.Focus());

        public void BlurInput() => commandInput.Blur();

        private void OnKeyDown(KeyDownEvent keyEvent)
        {
            if (keyEvent.keyCode == KeyCode.Return || keyEvent.keyCode == KeyCode.KeypadEnter)
            {
                var line = commandInput.value;
                commandInput.value = "";
                LineSubmitted?.Invoke(line);
                FocusInput();
                keyEvent.StopPropagation();
                return;
            }

            if (keyEvent.keyCode == KeyCode.UpArrow)
            {
                HistoryPreviousRequested?.Invoke();
                keyEvent.StopPropagation();
                return;
            }

            if (keyEvent.keyCode == KeyCode.DownArrow)
            {
                HistoryNextRequested?.Invoke();
                keyEvent.StopPropagation();
            }
        }

        private void OnScrolled(float value)
            => pinnedToBottom = value >= outputScroll.verticalScroller.highValue - PinThreshold;

        private void OnOutputGeometryChanged(GeometryChangedEvent geometryEvent)
        {
            if (pinnedToBottom)
            {
                outputScroll.schedule.Execute(ScrollToBottom);
            }
        }

        private void ScrollToBottom()
            => outputScroll.scrollOffset = new Vector2(outputScroll.scrollOffset.x, float.MaxValue);
    }
}
