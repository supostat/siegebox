using System;
using System.Collections.Generic;
using System.Text;
using Siegebox.Documentation;
using Siegebox.Process;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Wires one pipeline: pipes between neighbours first, redirects on top (in order, last
    /// wins), every descriptor table built before any spawn so no first output is ever lost.
    /// A failed redirect or unknown command becomes a MessageProcess stage, keeping sibling
    /// stages and the pipe cascade alive.
    /// </summary>
    internal sealed class PipelineAssembler
    {
        private static readonly PermissionMode CreateMode = new PermissionMode(0b110_100_100);

        private readonly Scheduler scheduler;
        private readonly VirtualFileSystem vfs;
        private readonly CommandRegistry commands;
        private readonly BuiltinRegistry builtins;
        private readonly Manual manual;
        private readonly ArgumentExpander expander;
        private readonly ExecutableResolver executableResolver;

        public PipelineAssembler(
            Scheduler scheduler,
            VirtualFileSystem vfs,
            CommandRegistry commands,
            BuiltinRegistry builtins,
            Manual manual,
            ArgumentExpander expander)
        {
            this.scheduler = scheduler;
            this.vfs = vfs;
            this.commands = commands;
            this.builtins = builtins;
            this.manual = manual ?? throw new ArgumentNullException(nameof(manual));
            this.expander = expander;
            executableResolver = new ExecutableResolver(vfs);
        }

        public LaunchedPipeline Launch(
            PipelineNode pipeline,
            ShellSession session,
            IByteStream defaultInput,
            IByteStream defaultOutput,
            IByteStream defaultError)
        {
            var stageCount = pipeline.Commands.Count;
            var pipes = CreatePipes(stageCount - 1);
            var stages = new IProcess[stageCount];
            var names = new string[stageCount];
            for (var index = 0; index < stageCount; index++)
            {
                var standardInput = index == 0 ? defaultInput : pipes[index - 1];
                var standardOutput = index == stageCount - 1 ? defaultOutput : pipes[index];
                stages[index] = BuildStage(
                    pipeline, pipeline.Commands[index], session,
                    ref standardInput, ref standardOutput, defaultError, out names[index]);
                ReleaseOrphanedPipeEnds(pipes, index, stageCount, standardInput, standardOutput);
            }

            return new LaunchedPipeline(SpawnAll(stages, names), DescriptionOf(pipeline));
        }

        private static PipeStream[] CreatePipes(int count)
        {
            var pipes = new PipeStream[count];
            for (var index = 0; index < count; index++)
            {
                pipes[index] = new PipeStream();
            }

            return pipes;
        }

        private int[] SpawnAll(IProcess[] stages, string[] names)
        {
            var pids = new int[stages.Length];
            for (var index = 0; index < stages.Length; index++)
            {
                pids[index] = scheduler.Spawn(stages[index], names[index]);
            }

            return pids;
        }

        private IProcess BuildStage(
            PipelineNode pipeline,
            CommandNode command,
            ShellSession session,
            ref IByteStream standardInput,
            ref IByteStream standardOutput,
            IByteStream standardError,
            out string name)
        {
            var argv = expander.Expand(command.Words);
            name = argv[0].Length == 0 ? "sh" : argv[0];
            var redirectError = ApplyRedirections(command, session, ref standardInput, ref standardOutput);
            var context = session.CreateContext(new FileDescriptorTable(standardInput, standardOutput, standardError));
            if (redirectError is not null)
            {
                return new MessageProcess(context, redirectError, FileDescriptorTable.Stderr, 1);
            }

            return CreateStageProcess(pipeline, argv, session, context);
        }

        private string? ApplyRedirections(
            CommandNode command,
            ShellSession session,
            ref IByteStream standardInput,
            ref IByteStream standardOutput)
        {
            foreach (var redirection in command.Redirections)
            {
                var target = ShellPath.Absolute(session.WorkingDirectory, expander.ExpandWord(redirection.TargetWord));
                try
                {
                    switch (redirection.Kind)
                    {
                        case RedirectionKind.In:
                            standardInput = vfs.Open(target, OpenMode.Read, session.Credentials);
                            break;
                        case RedirectionKind.Out:
                            standardOutput = vfs.OpenForWrite(target, WriteBehavior.Truncate, CreateMode, session.Credentials);
                            break;
                        default:
                            standardOutput = vfs.OpenForWrite(target, WriteBehavior.Append, CreateMode, session.Credentials);
                            break;
                    }
                }
                catch (VfsException error)
                {
                    return $"sh: {error.Path}: {VfsErrorText.MessageFor(error.Error)}\n";
                }
            }

            return null;
        }

        private IProcess CreateStageProcess(
            PipelineNode pipeline,
            IReadOnlyList<string> argv,
            ShellSession session,
            ExecutionContext context)
        {
            if (argv[0].Length == 0)
            {
                return new MessageProcess(context, "sh: : command not found\n", FileDescriptorTable.Stderr, 127);
            }

            var arguments = ArgumentsOf(argv);
            if (builtins.TryGet(argv[0], out var builtin))
            {
                var liveTarget = pipeline.Commands.Count == 1 && !pipeline.Background;
                return new BuiltinProcess(builtin, liveTarget ? session : session.Clone(), arguments, context);
            }

            if (commands.TryGet(argv[0], out var command))
            {
                if (RequestsHelp(arguments) && manual.TryGet(argv[0], out var page))
                {
                    return new MessageProcess(context, page.Synopsis + "\n", FileDescriptorTable.Stdout, 0);
                }

                var elevated = executableResolver.Resolve(argv[0], session.Credentials);
                var effectiveContext = elevated is null
                    ? context
                    : new ExecutionContext(
                        context.WorkingDirectory, elevated, context.Environment,
                        context.FileDescriptors, session.Credentials);
                return command.CreateProcess(effectiveContext, arguments);
            }

            return new MessageProcess(context, $"sh: {argv[0]}: command not found\n", FileDescriptorTable.Stderr, 127);
        }

        /// <summary>
        /// A redirect that displaces a pipe end leaves that end without any owner; closing it
        /// here keeps the EOF / broken-pipe cascade alive for the neighbour stage (POSIX: the
        /// displaced descriptor is simply never held).
        /// </summary>
        private static void ReleaseOrphanedPipeEnds(
            PipeStream[] pipes,
            int index,
            int stageCount,
            IByteStream finalInput,
            IByteStream finalOutput)
        {
            if (index > 0 && !ReferenceEquals(finalInput, pipes[index - 1]))
            {
                pipes[index - 1].CloseRead();
            }

            if (index < stageCount - 1 && !ReferenceEquals(finalOutput, pipes[index]))
            {
                pipes[index].CloseWrite();
            }
        }

        private static bool RequestsHelp(IReadOnlyList<string> arguments)
        {
            for (var index = 0; index < arguments.Count; index++)
            {
                if (arguments[index] == "--help")
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<string> ArgumentsOf(IReadOnlyList<string> argv)
        {
            var arguments = new List<string>(argv.Count - 1);
            for (var index = 1; index < argv.Count; index++)
            {
                arguments.Add(argv[index]);
            }

            return arguments;
        }

        private static string DescriptionOf(PipelineNode pipeline)
        {
            var description = new StringBuilder();
            for (var index = 0; index < pipeline.Commands.Count; index++)
            {
                if (index > 0)
                {
                    description.Append(" | ");
                }

                description.Append(string.Join(" ", pipeline.Commands[index].Words));
            }

            return description.ToString();
        }
    }
}
