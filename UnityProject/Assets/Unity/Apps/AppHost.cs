using System;
using Siegebox.App;
using UnityEngine;

namespace Siegebox.Unity
{
    /// <summary>
    /// The sole bridge from launch requests to windows: materializes the descriptor's app,
    /// wraps it into window content, and opens it through the window manager with the
    /// launch hook already run. A factory may only hand out fresh instances.
    /// </summary>
    public sealed class AppHost : IAppLauncher
    {
        private readonly WindowManager windowManager;

        public AppHost(WindowManager windowManager)
        {
            this.windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        }

        public void Launch(AppDescriptor descriptor)
        {
            if (descriptor is null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            try
            {
                var app = descriptor.CreateInstance();
                if (app.State != AppState.Created)
                {
                    throw new InvalidOperationException($"App '{descriptor.Id}' factory returned an already launched instance.");
                }

                var content = ContentFor(descriptor, app);
                app.OnLaunched();
                windowManager.Open(content);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                throw;
            }
        }

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
    }
}
