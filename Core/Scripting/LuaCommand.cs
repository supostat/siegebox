using System;
using System.Collections.Generic;
using System.Text;
using MoonSharp.Interpreter;
using Siegebox.Process;
using Siegebox.Shell;
using Siegebox.Vfs;

namespace Siegebox.Scripting
{
    /// <summary>
    /// A shell command whose behavior is a Lua handler. The handler receives a ctx table
    /// with args, write, write_err, read and vfs; read yields cooperatively so the process
    /// sleeps instead of blocking a tick, and decodes UTF-8 statefully so a multibyte
    /// character split across chunks survives intact. ctx.vfs (read/write/list) is mediated
    /// under the calling process's own credentials — never root — so a scripted command has
    /// exactly the file access the equivalent C# command has. Every Lua step is contained:
    /// script errors, budget exhaustion and unexpected failures all become exit code 1
    /// with one stderr line. Total output is capped at
    /// <see cref="LuaCommandProcess.OutputCapBytes"/>.
    /// </summary>
    public sealed class LuaCommand : ICommand
    {
        private readonly LuaHost host;
        private readonly DynValue handler;
        private readonly VirtualFileSystem vfs;

        internal LuaCommand(string name, LuaHost host, DynValue handler, VirtualFileSystem vfs)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
        }

        public string Name { get; }

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

            return new LuaCommandProcess(context, Name, host, handler, vfs, arguments);
        }

        private sealed class OutputCapExceededException : Exception
        {
        }

        private sealed class LuaCommandProcess : IProcess
        {
            public const int OutputCapBytes = 1_048_576;

            private const int ReadChunkBytes = 4096;
            private const string ReadRequestTag = "read";

            private static readonly DynValue[] ReadRequest = { DynValue.NewString(ReadRequestTag) };

            private readonly string commandName;
            private readonly LuaHost host;
            private readonly DynValue handler;
            private readonly VirtualFileSystem vfs;
            private readonly IReadOnlyList<string> arguments;
            private readonly Decoder stdinDecoder = Encoding.UTF8.GetDecoder();
            private readonly char[] decodedStdin = new char[Encoding.UTF8.GetMaxCharCount(ReadChunkBytes)];
            private PendingWriteQueue pendingWrites = new PendingWriteQueue();
            private LuaCall? call;
            private DynValue resumeValue = DynValue.Nil;
            private bool awaitingInput;
            private bool draining;
            private bool handlerExecuting;
            private long totalOutputBytes;

            public LuaCommandProcess(
                ExecutionContext context,
                string commandName,
                LuaHost host,
                DynValue handler,
                VirtualFileSystem vfs,
                IReadOnlyList<string> arguments)
            {
                Context = context;
                this.commandName = commandName;
                this.host = host;
                this.handler = handler;
                this.vfs = vfs;
                this.arguments = arguments;
            }

            public ExecutionContext Context { get; }

            public int ExitCode { get; private set; }

            public WakeCondition WakeCondition { get; private set; }

            public ProcessState Step()
            {
                try
                {
                    return draining ? DrainPendingWrites() : StepLua();
                }
                catch (LuaBudgetExceededException)
                {
                    return FailWith($"{commandName}: instruction budget exceeded\n");
                }
                catch (OutputCapExceededException)
                {
                    return FailWith($"{commandName}: output limit exceeded\n");
                }
                catch (InterpreterException scriptError)
                {
                    return FailWith($"{commandName}: lua error: {scriptError.DecoratedMessage ?? scriptError.Message}\n");
                }
                catch (Exception)
                {
                    return FailWith($"{commandName}: internal error\n");
                }
            }

            private ProcessState StepLua()
            {
                call ??= host.BeginCall(handler, new[] { BuildContextTable() }, commandName);
                if (awaitingInput && !TryFinishRead())
                {
                    WakeCondition = WakeCondition.Readable(Stdin);
                    return ProcessState.Sleeping;
                }

                LuaCall.StepResult step;
                handlerExecuting = true;
                try
                {
                    step = call.Step(resumeValue);
                }
                finally
                {
                    handlerExecuting = false;
                }

                resumeValue = DynValue.Nil;
                switch (step.Kind)
                {
                    case LuaCall.StepKind.SliceExhausted:
                        return ProcessState.Running;
                    case LuaCall.StepKind.Yielded:
                        RequireReadRequest(step.Value);
                        awaitingInput = true;
                        return ProcessState.Running;
                    default:
                        return CompleteWith(step.Value);
                }
            }

            private bool TryFinishRead()
            {
                var buffer = new byte[ReadChunkBytes];
                var result = Stdin.Read(buffer, 0, buffer.Length);
                switch (result.Status)
                {
                    case StreamStatus.Ok:
                        var decodedCount = stdinDecoder.GetChars(buffer, 0, result.Count, decodedStdin, 0);
                        resumeValue = DynValue.NewString(new string(decodedStdin, 0, decodedCount));
                        break;
                    case StreamStatus.Eof:
                        resumeValue = DynValue.Nil;
                        break;
                    default:
                        return false;
                }

                awaitingInput = false;
                return true;
            }

            private ProcessState CompleteWith(DynValue result)
            {
                ExitCode = CoerceExitCode(result);
                draining = true;
                return DrainPendingWrites();
            }

            private int CoerceExitCode(DynValue result)
            {
                var single = result.Type == DataType.Tuple
                    ? (result.Tuple.Length > 0 ? result.Tuple[0] : DynValue.Nil)
                    : result;
                if (single.Type == DataType.Void || single.Type == DataType.Nil)
                {
                    return 0;
                }

                if (single.Type == DataType.Number)
                {
                    return (int)single.Number;
                }

                pendingWrites.Enqueue(Stderr, $"{commandName}: handler returned a non-numeric exit code\n");
                return 1;
            }

            private ProcessState DrainPendingWrites()
            {
                if (pendingWrites.Drain() == DrainStatus.WouldBlock)
                {
                    WakeCondition = WakeCondition.Writable(pendingWrites.BlockedTarget!);
                    return ProcessState.Sleeping;
                }

                return ProcessState.Finished;
            }

            private ProcessState FailWith(string message)
            {
                ExitCode = 1;
                draining = true;
                pendingWrites = new PendingWriteQueue();
                pendingWrites.Enqueue(Stderr, message);
                return DrainPendingWrites();
            }

            private DynValue BuildContextTable()
            {
                var script = host.Script;
                var argsTable = new Table(script);
                for (var index = 0; index < arguments.Count; index++)
                {
                    argsTable.Set(index + 1, DynValue.NewString(arguments[index]));
                }

                var contextTable = new Table(script);
                contextTable.Set("args", DynValue.NewTable(argsTable));
                contextTable.Set("write", DynValue.NewCallback((callbackContext, callbackArguments) => Write(Stdout, callbackArguments, "write")));
                contextTable.Set("write_err", DynValue.NewCallback((callbackContext, callbackArguments) => Write(Stderr, callbackArguments, "write_err")));
                contextTable.Set("read", DynValue.NewCallback((callbackContext, callbackArguments) => DynValue.NewYieldReq(ReadRequest)));
                contextTable.Set("vfs", DynValue.NewTable(LuaVfsBridge.Build(script, vfs, Context.Credentials, EnsureHandlerExecuting)));
                return DynValue.NewTable(contextTable);
            }

            private DynValue Write(IByteStream target, CallbackArguments callbackArguments, string functionName)
            {
                var text = callbackArguments.AsType(0, functionName, DataType.String, false).String;
                totalOutputBytes += Encoding.UTF8.GetByteCount(text);
                if (totalOutputBytes > OutputCapBytes)
                {
                    throw new OutputCapExceededException();
                }

                pendingWrites.Enqueue(target, text);
                return DynValue.Nil;
            }

            private void EnsureHandlerExecuting()
            {
                if (!handlerExecuting)
                {
                    throw new ScriptRuntimeException("ctx.vfs is only available while the command runs");
                }
            }

            private static void RequireReadRequest(DynValue yielded)
            {
                var single = yielded.Type == DataType.Tuple && yielded.Tuple.Length > 0 ? yielded.Tuple[0] : yielded;
                if (single.Type != DataType.String || single.String != ReadRequestTag)
                {
                    throw new InvalidOperationException("A lua command yielded an unknown request.");
                }
            }

            private IByteStream Stdin => Context.FileDescriptors.Get(FileDescriptorTable.Stdin);

            private IByteStream Stdout => Context.FileDescriptors.Get(FileDescriptorTable.Stdout);

            private IByteStream Stderr => Context.FileDescriptors.Get(FileDescriptorTable.Stderr);
        }
    }
}
