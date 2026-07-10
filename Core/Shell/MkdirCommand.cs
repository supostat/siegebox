using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class MkdirCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public MkdirCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "mkdir";

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

            return new MkdirProcess(context, vfs, arguments);
        }

        private sealed class MkdirProcess : BufferedCommandProcess
        {
            private static readonly PermissionMode DirectoryMode = new PermissionMode(0b111_101_101);

            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public MkdirProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "mkdir";

            protected override CommandOutcome Run()
            {
                if (arguments.Count == 0)
                {
                    return CommandOutcome.Fail(1, "mkdir: missing operand\n");
                }

                foreach (var argument in arguments)
                {
                    var absolute = ShellPath.Absolute(Context.WorkingDirectory, argument);
                    vfs.CreateDirectory(absolute, DirectoryMode, Context.Credentials);
                }

                return CommandOutcome.Ok();
            }
        }
    }
}
