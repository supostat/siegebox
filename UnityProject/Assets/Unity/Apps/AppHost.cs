using System;
using System.Collections.Generic;
using Siegebox.App;
using Siegebox.Persistence;
using UnityEngine;

namespace Siegebox.Unity
{
    /// <summary>
    /// The sole bridge from launch requests to windows: materializes the descriptor's app,
    /// wraps it into window content, and opens it through the window manager with the launch
    /// hook already run. Tracks which app backs which window so a save can capture the running
    /// layout and a load can rehydrate it. A factory may only hand out fresh instances.
    /// </summary>
    public sealed class AppHost : IAppLauncher
    {
        private readonly WindowManager windowManager;
        private readonly Dictionary<Window, LiveApp> liveApps = new Dictionary<Window, LiveApp>();

        public AppHost(WindowManager windowManager)
        {
            this.windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            windowManager.WindowClosed += Forget;
        }

        public void Launch(AppDescriptor descriptor)
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            Open(descriptor, null, content => windowManager.Open(content));
        }

        public void Rehydrate(AppDescriptor descriptor, WindowSnapshot snapshot)
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            if (snapshot is null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var geometry = new Rect(snapshot.X, snapshot.Y, snapshot.Width, snapshot.Height);
            Open(descriptor, snapshot, content => windowManager.OpenAt(content, geometry, ToWindowState(snapshot.State), snapshot.Focused));
        }

        public List<WindowSnapshot> Capture()
        {
            var ordered = windowManager.WindowsByZOrder();
            var snapshots = new List<WindowSnapshot>(ordered.Count);
            for (var zOrderIndex = 0; zOrderIndex < ordered.Count; zOrderIndex++)
            {
                var window = ordered[zOrderIndex];
                if (!liveApps.TryGetValue(window, out var liveApp))
                {
                    continue;
                }

                var geometry = window.Geometry;
                snapshots.Add(new WindowSnapshot
                {
                    AppId = liveApp.AppId,
                    X = geometry.x,
                    Y = geometry.y,
                    Width = geometry.width,
                    Height = geometry.height,
                    State = ToDisplayState(window.State),
                    ZOrderIndex = zOrderIndex,
                    Focused = windowManager.FocusedWindow == window,
                    AppState = (liveApp.App as IPersistentApp)?.CaptureState()
                });
            }

            return snapshots;
        }

        private void Open(AppDescriptor descriptor, WindowSnapshot snapshot, Func<IWindowContent, Window> openWindow)
        {
            try
            {
                var app = descriptor.CreateInstance();
                if (app.State != AppState.Created)
                {
                    throw new InvalidOperationException($"App '{descriptor.Id}' factory returned an already launched instance.");
                }

                var content = ContentFor(descriptor, app);
                app.OnLaunched();
                if (snapshot?.AppState != null && app is IPersistentApp persistentApp)
                {
                    persistentApp.RestoreState(snapshot.AppState);
                }

                var window = openWindow(content);
                liveApps[window] = new LiveApp(descriptor.Id, app);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                throw;
            }
        }

        private void Forget(Window window) => liveApps.Remove(window);

        private static IWindowContent ContentFor(AppDescriptor descriptor, IApp app)
        {
            if (app is IWindowContent content)
            {
                return content;
            }

            if (app is ITextContentApp textApp)
            {
                return new TextAppContent(app, textApp);
            }

            throw new InvalidOperationException($"App '{descriptor.Id}' does not provide window content.");
        }

        private static WindowState ToWindowState(WindowDisplayState state) => state switch
        {
            WindowDisplayState.Minimized => WindowState.Minimized,
            WindowDisplayState.Maximized => WindowState.Maximized,
            _ => WindowState.Normal
        };

        private static WindowDisplayState ToDisplayState(WindowState state) => state switch
        {
            WindowState.Minimized => WindowDisplayState.Minimized,
            WindowState.Maximized => WindowDisplayState.Maximized,
            _ => WindowDisplayState.Normal
        };

        private sealed class LiveApp
        {
            public LiveApp(string appId, IApp app)
            {
                AppId = appId;
                App = app;
            }

            public string AppId { get; }

            public IApp App { get; }
        }
    }
}
