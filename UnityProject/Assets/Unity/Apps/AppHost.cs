using System;
using Siegebox.App;
using UnityEngine;

namespace Siegebox.Unity
{
    /// <summary>
    /// The sole bridge from launch requests to windows: materializes the descriptor's app,
    /// requires it to provide window content, and opens it through the window manager with
    /// the launch hook already run.
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
                if (!(app is IWindowContent content))
                {
                    throw new InvalidOperationException($"App '{descriptor.Id}' does not provide window content.");
                }

                app.OnLaunched();
                windowManager.Open(content);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                throw;
            }
        }
    }
}
