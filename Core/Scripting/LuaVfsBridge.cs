using System;
using System.IO;
using System.Text;
using MoonSharp.Interpreter;
using Siegebox.Vfs;

namespace Siegebox.Scripting
{
    /// <summary>
    /// Builds the Lua 'vfs' table (read/write/list) bound to one identity. Every access
    /// goes through the resolver under the supplied <see cref="Credentials"/> — never an
    /// ambient or global root — so a command's file access is mediated exactly like the
    /// equivalent C# command. The optional <paramref name="ensureAvailable"/> guard runs
    /// before every access and throws when the table must not be used: the load table
    /// closes after the mod loads, and a command table refuses once its command is no
    /// longer executing, so a handle stashed for later or cross-process use is dead.
    /// VfsException surfaces as a pcall-able error.
    /// </summary>
    internal static class LuaVfsBridge
    {
        private static readonly PermissionMode DefaultFileMode = new PermissionMode(0b110_100_100);

        public static Table Build(
            Script script,
            VirtualFileSystem vfs,
            Credentials credentials,
            Action? ensureAvailable = null)
        {
            var table = new Table(script);
            table.Set("read", DynValue.NewCallback((context, arguments) => Read(vfs, credentials, ensureAvailable, arguments)));
            table.Set("write", DynValue.NewCallback((context, arguments) => Write(vfs, credentials, ensureAvailable, arguments)));
            table.Set("list", DynValue.NewCallback((context, arguments) => List(script, vfs, credentials, ensureAvailable, arguments)));
            return table;
        }

        private static DynValue Read(VirtualFileSystem vfs, Credentials credentials, Action? ensureAvailable, CallbackArguments arguments)
        {
            ensureAvailable?.Invoke();
            var path = arguments.AsType(0, "vfs.read", DataType.String, false).String;
            return DynValue.NewString(Guard(path, () =>
            {
                var stream = vfs.Open(path, OpenMode.Read, credentials);
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

        private static DynValue Write(VirtualFileSystem vfs, Credentials credentials, Action? ensureAvailable, CallbackArguments arguments)
        {
            ensureAvailable?.Invoke();
            var path = arguments.AsType(0, "vfs.write", DataType.String, false).String;
            var text = arguments.AsType(1, "vfs.write", DataType.String, false).String;
            Guard<object?>(path, () =>
            {
                var stream = vfs.OpenForWrite(path, WriteBehavior.Truncate, DefaultFileMode, credentials);
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

        private static DynValue List(Script script, VirtualFileSystem vfs, Credentials credentials, Action? ensureAvailable, CallbackArguments arguments)
        {
            ensureAvailable?.Invoke();
            var path = arguments.AsType(0, "vfs.list", DataType.String, false).String;
            var names = Guard(path, () => vfs.List(path, credentials));
            var array = new Table(script);
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

        private static T Guard<T>(string path, Func<T> operation)
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
    }
}
