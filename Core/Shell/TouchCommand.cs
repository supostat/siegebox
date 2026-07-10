using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class TouchCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public TouchCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "touch";

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

            return new TouchProcess(context, vfs, arguments);
        }

        private sealed class TouchProcess : BufferedCommandProcess
        {
            private static readonly PermissionMode FileMode = new PermissionMode(0b110_100_100);

            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public TouchProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "touch";

            protected override CommandOutcome Run()
            {
                if (arguments.Count == 0)
                {
                    return CommandOutcome.Fail(1, "touch: missing operand\n");
                }

                foreach (var argument in arguments)
                {
                    EnsureExists(ShellPath.Absolute(Context.WorkingDirectory, argument));
                }

                return CommandOutcome.Ok();
            }

            private void EnsureExists(string absolute)
            {
                try
                {
                    vfs.Stat(absolute, Context.Credentials);
                }
                catch (VfsException failure) when (failure.Error == VfsError.ENOENT)
                {
                    vfs.CreateFile(absolute, FileMode, Context.Credentials);
                }
            }
        }
    }
}
