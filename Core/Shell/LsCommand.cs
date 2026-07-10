using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class LsCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public LsCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "ls";

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

            return new LsProcess(context, vfs, arguments);
        }

        private sealed class LsProcess : BufferedCommandProcess
        {
            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public LsProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "ls";

            protected override CommandOutcome Run()
            {
                var targets = arguments.Count == 0
                    ? new List<string> { Context.WorkingDirectory }
                    : new List<string>(arguments);
                var output = new StringBuilder();
                var error = new StringBuilder();
                for (var index = 0; index < targets.Count; index++)
                {
                    try
                    {
                        AppendTarget(output, targets[index], index, targets.Count > 1);
                    }
                    catch (VfsException failure)
                    {
                        error.Append($"ls: {failure.Path}: {VfsErrorText.MessageFor(failure.Error)}\n");
                    }
                }

                return error.Length == 0
                    ? CommandOutcome.Ok(output.ToString())
                    : CommandOutcome.PartialFail(1, output.ToString(), error.ToString());
            }

            private void AppendTarget(StringBuilder output, string target, int index, bool multiple)
            {
                var absolute = ShellPath.Absolute(Context.WorkingDirectory, target);
                var info = vfs.Stat(absolute, Context.Credentials);
                if (multiple)
                {
                    if (index > 0)
                    {
                        output.Append('\n');
                    }

                    output.Append(target).Append(":\n");
                }

                if (info.Type == NodeType.Directory)
                {
                    foreach (var name in vfs.List(absolute, Context.Credentials))
                    {
                        output.Append(name).Append('\n');
                    }
                }
                else
                {
                    output.Append(target).Append('\n');
                }
            }
        }
    }
}
