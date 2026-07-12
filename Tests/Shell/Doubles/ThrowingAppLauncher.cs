using System;
using Siegebox.App;

namespace Siegebox.Shell.Tests
{
    /// <summary>IAppLauncher whose every launch fails, like a broken mod app.</summary>
    internal sealed class ThrowingAppLauncher : IAppLauncher
    {
        public void Launch(AppDescriptor descriptor)
            => throw new InvalidOperationException($"App '{descriptor.Id}' is broken.");
    }
}
