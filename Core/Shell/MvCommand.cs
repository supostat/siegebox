using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class MvCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public MvCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "mv";

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

            return new MvProcess(context, vfs, arguments);
        }

        private sealed class MvProcess : BufferedCommandProcess
        {
            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public MvProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "mv";

            protected override CommandOutcome Run()
            {
                if (arguments.Count != 2)
                {
                    return CommandOutcome.Fail(1, "mv: usage: mv source destination\n");
                }

                var source = ShellPath.Absolute(Context.WorkingDirectory, arguments[0]);
                var destination = ShellPath.Absolute(Context.WorkingDirectory, arguments[1]);
                vfs.Move(source, destination, Context.Credentials);
                return CommandOutcome.Ok();
            }
        }
    }
}
