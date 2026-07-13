using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Process;
using Siegebox.Security;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    public sealed class LsCommand : ICommand
    {
        private const string LongFlag = "-l";

        private readonly VirtualFileSystem vfs;
        private readonly AuthenticationService authentication;

        public LsCommand(VirtualFileSystem vfs, AuthenticationService authentication)
        {
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            this.authentication = authentication ?? throw new ArgumentNullException(nameof(authentication));
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

            return new LsProcess(context, vfs, authentication, arguments);
        }

        private sealed class LsProcess : BufferedCommandProcess
        {
            private readonly VirtualFileSystem vfs;
            private readonly AuthenticationService authentication;
            private readonly IReadOnlyList<string> arguments;

            public LsProcess(ExecutionContext context, VirtualFileSystem vfs, AuthenticationService authentication, IReadOnlyList<string> arguments)
                : base(context)
            {
                this.vfs = vfs;
                this.authentication = authentication;
                this.arguments = arguments;
            }

            protected override string CommandName => "ls";

            protected override CommandOutcome Run()
            {
                var targets = ParseTargets(out var longFormat);
                var output = new StringBuilder();
                var error = new StringBuilder();
                for (var index = 0; index < targets.Count; index++)
                {
                    try
                    {
                        AppendTarget(output, targets[index], index, targets.Count > 1, longFormat);
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

            private List<string> ParseTargets(out bool longFormat)
            {
                longFormat = false;
                var targets = new List<string>();
                foreach (var argument in arguments)
                {
                    if (argument == LongFlag)
                    {
                        longFormat = true;
                    }
                    else
                    {
                        targets.Add(argument);
                    }
                }

                if (targets.Count == 0)
                {
                    targets.Add(Context.WorkingDirectory);
                }

                return targets;
            }

            private void AppendTarget(StringBuilder output, string target, int index, bool multiple, bool longFormat)
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
                    AppendDirectory(output, absolute, longFormat);
                }
                else if (longFormat)
                {
                    output.Append(FormatLong(info, target));
                }
                else
                {
                    output.Append(target).Append('\n');
                }
            }

            private void AppendDirectory(StringBuilder output, string absolute, bool longFormat)
            {
                foreach (var name in vfs.List(absolute, Context.Credentials))
                {
                    if (longFormat)
                    {
                        var childInfo = vfs.Stat(ShellPath.Absolute(absolute, name), Context.Credentials);
                        output.Append(FormatLong(childInfo, name));
                    }
                    else
                    {
                        output.Append(name).Append('\n');
                    }
                }
            }

            private string FormatLong(VfsEntryInfo info, string name)
                => $"{TypeCharacter(info.Type)}{info.Mode}  {OwnerName(info.OwnerUid)}  {info.GroupGid}  {info.Size}  {name}\n";

            private string OwnerName(int ownerUid)
                => authentication.TryResolveByUid(ownerUid, out var record) ? record.Name : ownerUid.ToString();

            private static char TypeCharacter(NodeType type) => type switch
            {
                NodeType.Directory => 'd',
                NodeType.Symlink => 'l',
                NodeType.Device => 'c',
                _ => '-'
            };
        }
    }
}
