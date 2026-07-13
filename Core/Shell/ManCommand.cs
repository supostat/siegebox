using System;
using System.Collections.Generic;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Prints a manual page by reading <c>/usr/share/man/&lt;name&gt;</c> through the resolver under
    /// the caller's identity, so page permissions are enforced exactly like <c>cat</c>. A name
    /// containing a path separator is rejected as not-found rather than escaping the man tree.
    /// </summary>
    public sealed class ManCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public ManCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "man";

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

            return new ManProcess(context, vfs, arguments);
        }

        private sealed class ManProcess : BufferedCommandProcess
        {
            private const string ManDirectory = "/usr/share/man/";

            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public ManProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "man";

            protected override CommandOutcome Run()
            {
                if (arguments.Count == 0)
                {
                    return CommandOutcome.Fail(1, "man: missing operand\n");
                }

                var name = arguments[0];
                if (name.IndexOf('/') >= 0)
                {
                    return CommandOutcome.Fail(1, $"man: {name}: {VfsErrorText.MessageFor(VfsError.ENOENT)}\n");
                }

                var stream = vfs.Open(ManDirectory + name, OpenMode.Read, Context.Credentials);
                try
                {
                    return CommandOutcome.Ok(ByteStreamText.ReadToEnd(stream));
                }
                finally
                {
                    stream.CloseRead();
                }
            }
        }
    }
}
