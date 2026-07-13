using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Siegebox.Unity
{
    /// <summary>
    /// The desktop-wide system panel: shows the owning session's identity, a live clock
    /// driven by the UI Toolkit panel scheduler (never the kernel scheduler), and a power
    /// control that quits the application. All OS-string labels render as plain text.
    /// </summary>
    public sealed class SystemPanel
    {
        private const string ClockFormat = "HH:mm";

        private readonly Label clock;

        public SystemPanel(VisualElement root, WindowIdentity owner)
        {
            if (root is null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var panelUser = root.Q<Label>("panel-user");
            panelUser.enableRichText = false;
            panelUser.text = owner.ChromeLabel;

            clock = root.Q<Label>("panel-clock");
            clock.enableRichText = false;

            var power = root.Q<Button>("power-button");
            power.clicked += () => Application.Quit();

            UpdateClock();
            root.schedule.Execute(UpdateClock).Every(1000);
        }

        private void UpdateClock() => clock.text = DateTime.Now.ToString(ClockFormat);
    }
}
