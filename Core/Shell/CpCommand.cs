using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class CpCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public CpCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "cp";

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

            return new CpProcess(context, vfs, arguments);
        }

        private sealed class CpProcess : BufferedCommandProcess
        {
            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public CpProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "cp";

            protected override CommandOutcome Run()
            {
                if (arguments.Count != 2)
                {
                    return CommandOutcome.Fail(1, "cp: usage: cp source destination\n");
                }

                var source = ShellPath.Absolute(Context.WorkingDirectory, arguments[0]);
                var destination = ShellPath.Absolute(Context.WorkingDirectory, arguments[1]);
                vfs.Copy(source, destination, Context.Credentials);
                return CommandOutcome.Ok();
            }
        }
    }
}
