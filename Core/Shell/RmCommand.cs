using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class RmCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public RmCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "rm";

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

            return new RmProcess(context, vfs, arguments);
        }

        private sealed class RmProcess : BufferedCommandProcess
        {
            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public RmProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "rm";

            protected override CommandOutcome Run()
            {
                if (arguments.Count == 0)
                {
                    return CommandOutcome.Fail(1, "rm: missing operand\n");
                }

                var error = new StringBuilder();
                foreach (var argument in arguments)
                {
                    try
                    {
                        vfs.Delete(ShellPath.Absolute(Context.WorkingDirectory, argument), Context.Credentials);
                    }
                    catch (VfsException failure)
                    {
                        error.Append($"rm: {failure.Path}: {VfsErrorText.MessageFor(failure.Error)}\n");
                    }
                }

                return error.Length == 0 ? CommandOutcome.Ok() : CommandOutcome.Fail(1, error.ToString());
            }
        }
    }
}
