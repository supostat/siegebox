using System;
using System.Collections.Generic;
using Siegebox.App;
using Siegebox.Process;

namespace Siegebox.Shell
{
    public sealed class OpenCommand : ICommand
    {
        private readonly AppRegistry apps;
        private readonly IAppLauncher launcher;

        public OpenCommand(AppRegistry apps, IAppLauncher launcher)
        {
            this.apps = apps ?? throw new ArgumentNullException(nameof(apps));
            this.launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        }

        public string Name => "open";

        public IProcess CreateProcess(ExecutionContext context, IReadOnlyList<string> arguments)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            return new OpenProcess(context, apps, launcher, arguments);
        }

        private sealed class OpenProcess : BufferedCommandProcess
        {
            private readonly AppRegistry apps;
            private readonly IAppLauncher launcher;
            private readonly IReadOnlyList<string> arguments;

            public OpenProcess(
                ExecutionContext context,
                AppRegistry apps,
                IAppLauncher launcher,
                IReadOnlyList<string> arguments)
                : base(context)
            {
                this.apps = apps;
                this.launcher = launcher;
                this.arguments = arguments;
            }

            protected override string CommandName => "open";

            protected override CommandOutcome Run()
            {
                if (arguments.Count != 1)
                {
                    return CommandOutcome.Fail(1, "open: usage: open app\n");
                }

                if (!apps.TryGet(arguments[0], out var descriptor))
                {
                    return CommandOutcome.Fail(1, $"open: {arguments[0]}: no such app\n");
                }

                try
                {
                    launcher.Launch(descriptor);
                }
                catch (Exception)
                {
                    return CommandOutcome.Fail(1, $"open: {arguments[0]}: launch failed\n");
                }

                return CommandOutcome.Ok();
            }
        }
    }
}
