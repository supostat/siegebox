using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// <c>chmod &lt;mode&gt; &lt;path&gt;...</c>: an octal mode (e.g. <c>4755</c>) or the symbolic
    /// setuid forms <c>u+s</c>/<c>u-s</c>, delegated to <see cref="VirtualFileSystem.Chmod"/>
    /// under the caller's credentials, which enforces owner-or-root and the setuid-only-root
    /// rule (a non-root <c>u+s</c> becomes EPERM). Only the setuid special bit is supported;
    /// setgid and sticky are rejected as invalid modes. A malformed spec or a missing operand is
    /// RETURNED as a failure, never thrown — a thrown non-VfsException would be swallowed into a
    /// false success by the buffered-process frame.
    /// </summary>
    public sealed class ChmodCommand : ICommand
    {
        private readonly VirtualFileSystem vfs;

        public ChmodCommand(VirtualFileSystem vfs)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name => "chmod";

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

            return new ChmodProcess(context, vfs, arguments);
        }

        private sealed class ChmodProcess : BufferedCommandProcess
        {
            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;

            public ChmodProcess(ExecutionContext context, VirtualFileSystem vfs, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.arguments = arguments;
            }

            protected override string CommandName => "chmod";

            protected override CommandOutcome Run()
            {
                if (arguments.Count < 2)
                {
                    return CommandOutcome.Fail(1, "chmod: missing operand\n");
                }

                var spec = arguments[0];
                if (TryParseOctal(spec, out var absoluteMode))
                {
                    return ApplyToPaths(absolute => vfs.Chmod(absolute, absoluteMode, Context.Credentials));
                }

                if (TryParseSetuidSymbolic(spec, out var setUid))
                {
                    return ApplyToPaths(absolute => vfs.Chmod(absolute, CurrentMode(absolute).WithSetUid(setUid), Context.Credentials));
                }

                return CommandOutcome.Fail(1, $"chmod: invalid mode: '{spec}'\n");
            }

            private CommandOutcome ApplyToPaths(Action<string> apply)
            {
                var errors = new StringBuilder();
                for (var index = 1; index < arguments.Count; index++)
                {
                    var absolute = ShellPath.Absolute(Context.WorkingDirectory, arguments[index]);
                    try
                    {
                        apply(absolute);
                    }
                    catch (VfsException failure)
                    {
                        errors.Append($"chmod: {failure.Path}: {VfsErrorText.MessageFor(failure.Error)}\n");
                    }
                }

                return errors.Length == 0
                    ? CommandOutcome.Ok()
                    : CommandOutcome.PartialFail(1, "", errors.ToString());
            }

            private PermissionMode CurrentMode(string absolute) => vfs.Stat(absolute, Context.Credentials).Mode;

            private static bool TryParseOctal(string spec, out PermissionMode mode)
            {
                mode = default;
                if (spec.Length == 0 || spec.Length > 4)
                {
                    return false;
                }

                var value = 0;
                foreach (var character in spec)
                {
                    if (character < '0' || character > '7')
                    {
                        return false;
                    }

                    value = (value * 8) + (character - '0');
                }

                var specialBits = value & ~PermissionMode.PermissionBitsMask;
                if (specialBits != 0 && specialBits != PermissionMode.SetUidBit)
                {
                    return false;
                }

                mode = new PermissionMode(value);
                return true;
            }

            private static bool TryParseSetuidSymbolic(string spec, out bool setUid)
            {
                switch (spec)
                {
                    case "u+s":
                        setUid = true;
                        return true;
                    case "u-s":
                        setUid = false;
                        return true;
                    default:
                        setUid = false;
                        return false;
                }
            }
        }
    }
}
