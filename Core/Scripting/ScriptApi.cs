using System;
using System.IO;
using System.Text;
using MoonSharp.Interpreter;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;
using Siegebox.Vfs;

namespace Siegebox.Scripting
{
    /// <summary>
    /// The stable, only capability surface mods see — 'siegebox' with register_command,
    /// register_app, register_file_type, subscribe, log and vfs.read/write/list — installed
    /// into a mod's host from plain callbacks (AOT-safe, no CLR marshaling). Every callback
    /// turns invalid Lua arguments and registry rejections into pcall-able errors;
    /// registrations are recorded in the mod's scope only after the registry accepts them,
    /// so a failed load rolls back exactly what was installed. Mod vfs access runs as uid 0
    /// until launch identities are threaded through the stack.
    /// </summary>
    public sealed class ScriptApi
    {
        private static readonly Credentials ModCredentials = new Credentials(0);
        private static readonly PermissionMode ModFileMode = new PermissionMode(0b110_100_100);

        private readonly CommandRegistry commands;
        private readonly AppRegistry apps;
        private readonly FileTypeRegistry fileTypes;
        private readonly VirtualFileSystem vfs;
        private readonly EventBus events;
        private readonly Action<string> log;

        public ScriptApi(
            CommandRegistry commands,
            AppRegistry apps,
            FileTypeRegistry fileTypes,
            VirtualFileSystem vfs,
            EventBus events,
            Action<string> log)
        {
            this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
            this.apps = apps ?? throw new ArgumentNullException(nameof(apps));
            this.fileTypes = fileTypes ?? throw new ArgumentNullException(nameof(fileTypes));
            this.vfs = vfs ?? throw new ArgumentNullException(nameof(vfs));
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public ModRegistrationScope CreateScope() => new ModRegistrationScope(commands, apps, fileTypes);

        public void InstallInto(LuaHost host, ModRegistrationScope scope)
        {
            if (host is null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            if (scope is null)
            {
                throw new ArgumentNullException(nameof(scope));
            }

            var script = host.Script;
            var siegebox = new Table(script);
            siegebox.Set("register_command", RegistrationCallback("register_command", arguments => RegisterCommand(host, scope, arguments)));
            siegebox.Set("register_app", RegistrationCallback("register_app", arguments => RegisterApp(host, scope, arguments)));
            siegebox.Set("register_file_type", RegistrationCallback("register_file_type", arguments => RegisterFileType(scope, arguments)));
            siegebox.Set("subscribe", DynValue.NewCallback((context, arguments) => Subscribe(host, scope, arguments)));
            siegebox.Set("log", DynValue.NewCallback((context, arguments) => Log(arguments)));
            siegebox.Set("vfs", DynValue.NewTable(BuildVfsTable(host)));
            script.Globals.Set("siegebox", DynValue.NewTable(siegebox));
        }

        private DynValue RegisterCommand(LuaHost host, ModRegistrationScope scope, CallbackArguments arguments)
        {
            var name = arguments.AsType(0, "register_command", DataType.String, false).String;
            RequireIdentifier(name, "register_command", "name");
            var handler = arguments.AsType(1, "register_command", DataType.Function, false);
            commands.Register(new LuaCommand(name, host, handler));
            scope.RecordCommand(name);
            return DynValue.Nil;
        }

        private DynValue RegisterApp(LuaHost host, ModRegistrationScope scope, CallbackArguments arguments)
        {
            var spec = arguments.AsType(0, "register_app", DataType.Table, false).Table;
            var id = Field(spec, "id", "register_app", DataType.String, true).String;
            RequireIdentifier(id, "register_app", "id");
            var nameField = Field(spec, "name", "register_app", DataType.String, false);
            var name = nameField.IsNil() ? id : nameField.String;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ScriptRuntimeException("register_app: field 'name' must not be blank");
            }

            var onLaunch = Field(spec, "on_launch", "register_app", DataType.Function, true);
            var onFocus = Field(spec, "on_focus", "register_app", DataType.Function, false);
            var onFocusLost = Field(spec, "on_focus_lost", "register_app", DataType.Function, false);
            var onClose = Field(spec, "on_close", "register_app", DataType.Function, false);
            apps.Register(new AppDescriptor(id, name, () => new LuaApp(name, host, onLaunch, onFocus, onFocusLost, onClose, log)));
            scope.RecordApp(id);
            return DynValue.Nil;
        }

        private DynValue RegisterFileType(ModRegistrationScope scope, CallbackArguments arguments)
        {
            var extension = arguments.AsType(0, "register_file_type", DataType.String, false).String;
            var appId = arguments.AsType(1, "register_file_type", DataType.String, false).String;
            var previous = fileTypes.TryGet(extension, out var previousAppId) ? previousAppId : null;
            fileTypes.Register(extension, appId);
            scope.RecordFileType(extension, previous);
            return DynValue.Nil;
        }

        private static DynValue RegistrationCallback(string functionName, Func<CallbackArguments, DynValue> register)
            => DynValue.NewCallback((context, arguments) =>
            {
                try
                {
                    return register(arguments);
                }
                catch (ArgumentException invalidRegistration)
                {
                    throw new ScriptRuntimeException($"{functionName}: {invalidRegistration.Message}");
                }
            });

        private DynValue Subscribe(LuaHost host, ModRegistrationScope scope, CallbackArguments arguments)
        {
            var eventName = arguments.AsType(0, "subscribe", DataType.String, false).String;
            var handler = arguments.AsType(1, "subscribe", DataType.Function, false);
            if (!IsKnownEventName(eventName))
            {
                throw new ScriptRuntimeException($"subscribe: unknown event '{eventName}'");
            }

            var subscription = events.Subscribe(
                eventName,
                kernelEvent => host.CallBounded(handler, new[] { EventToTable(host.Script, kernelEvent) }, $"subscribe:{eventName}"));
            scope.RecordSubscription(subscription);
            return DynValue.NewCallback((context, unsubscribeArguments) =>
            {
                subscription.Dispose();
                return DynValue.Nil;
            });
        }

        private DynValue Log(CallbackArguments arguments)
        {
            log(arguments.AsType(0, "log", DataType.String, false).String);
            return DynValue.Nil;
        }

        private Table BuildVfsTable(LuaHost host)
        {
            var vfsTable = new Table(host.Script);
            vfsTable.Set("read", DynValue.NewCallback((context, arguments) => VfsRead(arguments)));
            vfsTable.Set("write", DynValue.NewCallback((context, arguments) => VfsWrite(arguments)));
            vfsTable.Set("list", DynValue.NewCallback((context, arguments) => VfsList(host, arguments)));
            return vfsTable;
        }

        private DynValue VfsRead(CallbackArguments arguments)
        {
            var path = arguments.AsType(0, "vfs.read", DataType.String, false).String;
            return DynValue.NewString(GuardVfs(path, () =>
            {
                var stream = vfs.Open(path, OpenMode.Read, ModCredentials);
                try
                {
                    return ReadAllText(stream);
                }
                finally
                {
                    stream.CloseRead();
                }
            }));
        }

        private DynValue VfsWrite(CallbackArguments arguments)
        {
            var path = arguments.AsType(0, "vfs.write", DataType.String, false).String;
            var text = arguments.AsType(1, "vfs.write", DataType.String, false).String;
            GuardVfs<object?>(path, () =>
            {
                var stream = vfs.OpenForWrite(path, WriteBehavior.Truncate, ModFileMode, ModCredentials);
                try
                {
                    WriteAllText(stream, path, text);
                    return null;
                }
                finally
                {
                    stream.CloseWrite();
                }
            });
            return DynValue.Nil;
        }

        private DynValue VfsList(LuaHost host, CallbackArguments arguments)
        {
            var path = arguments.AsType(0, "vfs.list", DataType.String, false).String;
            var names = GuardVfs(path, () => vfs.List(path, ModCredentials));
            var array = new Table(host.Script);
            for (var index = 0; index < names.Count; index++)
            {
                array.Set(index + 1, DynValue.NewString(names[index]));
            }

            return DynValue.NewTable(array);
        }

        private static string ReadAllText(IByteStream stream)
        {
            var collected = new MemoryStream();
            var chunk = new byte[4096];
            while (true)
            {
                var result = stream.Read(chunk, 0, chunk.Length);
                if (result.Status != StreamStatus.Ok)
                {
                    return Encoding.UTF8.GetString(collected.ToArray());
                }

                collected.Write(chunk, 0, result.Count);
            }
        }

        private static void WriteAllText(IByteStream stream, string path, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            if (bytes.Length == 0)
            {
                return;
            }

            var result = stream.Write(bytes, 0, bytes.Length);
            if (result.Status != StreamStatus.Ok)
            {
                throw new ScriptRuntimeException($"vfs.write: '{path}' rejected the write");
            }
        }

        private static T GuardVfs<T>(string path, Func<T> operation)
        {
            try
            {
                return operation();
            }
            catch (VfsException error)
            {
                throw new ScriptRuntimeException($"{error.Error}: {error.Path}");
            }
        }

        private static bool IsKnownEventName(string eventName)
            => eventName == KernelEvent.ProcessSpawnedName
               || eventName == KernelEvent.ProcessExitedName
               || eventName == KernelEvent.FileOpenedName;

        private static DynValue EventToTable(Script script, KernelEvent kernelEvent)
        {
            var table = new Table(script);
            table.Set("name", DynValue.NewString(kernelEvent.Name));
            table.Set("pid", DynValue.NewNumber(kernelEvent.Pid));
            table.Set("process_name", DynValue.NewString(kernelEvent.ProcessName));
            table.Set("exit_code", DynValue.NewNumber(kernelEvent.ExitCode));
            table.Set("path", DynValue.NewString(kernelEvent.Path));
            table.Set("uid", DynValue.NewNumber(kernelEvent.Uid));
            table.Set("access", DynValue.NewString(kernelEvent.Access));
            return DynValue.NewTable(table);
        }

        private static void RequireIdentifier(string value, string functionName, string parameterName)
        {
            if (!ModIdentifier.IsValid(value))
            {
                throw new ScriptRuntimeException($"{functionName}: {parameterName} '{value}' must match {ModIdentifier.Rule}");
            }
        }

        private static DynValue Field(Table spec, string fieldName, string functionName, DataType expected, bool required)
        {
            var value = spec.Get(fieldName);
            if (value.IsNil() && !required)
            {
                return DynValue.Nil;
            }

            if (value.Type != expected)
            {
                throw new ScriptRuntimeException(
                    $"{functionName}: field '{fieldName}' must be a {expected.ToString().ToLowerInvariant()}");
            }

            return value;
        }
    }
}
