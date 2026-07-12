using System.Collections.Generic;
using Siegebox.App;

namespace Siegebox.Shell.Tests
{
    /// <summary>IAppLauncher that records every launched descriptor instead of opening windows.</summary>
    internal sealed class RecordingAppLauncher : IAppLauncher
    {
        public List<AppDescriptor> Launched { get; } = new List<AppDescriptor>();

        public void Launch(AppDescriptor descriptor) => Launched.Add(descriptor);
    }
}
