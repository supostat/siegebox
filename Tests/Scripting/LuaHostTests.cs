using System;
using MoonSharp.Interpreter;
using NUnit.Framework;
using Siegebox.Scripting;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class LuaHostTests
    {
        [Test]
        public void Chunk_defines_a_global_function_and_a_later_chunk_calls_it()
        {
            var host = new LuaHost();

            host.RunChunk("function add(a, b) return a + b end", "define");
            var result = host.RunChunk("return add(2, 3)", "call");

            Assert.That(result.Number, Is.EqualTo(5));
        }

        [Test]
        public void Runtime_error_message_carries_the_chunk_name()
        {
            var host = new LuaHost();

            var error = Assert.Throws<ScriptRuntimeException>(() => host.RunChunk("error('kaboom')", "mychunk"));

            Assert.That(error.DecoratedMessage, Does.Contain("mychunk"));
            Assert.That(error.DecoratedMessage, Does.Contain("kaboom"));
        }

        [Test]
        public void Syntax_error_throws_interpreter_exception_and_the_host_survives()
        {
            var host = new LuaHost();

            Assert.That(() => host.RunChunk("this is (not lua", "bad"), Throws.InstanceOf<InterpreterException>());
            Assert.That(host.RunChunk("return 1", "next").Number, Is.EqualTo(1));
        }

        [Test]
        public void Null_and_blank_arguments_are_rejected()
        {
            var host = new LuaHost();

            Assert.Throws<ArgumentNullException>(() => host.RunChunk(null, "chunk"));
            Assert.Throws<ArgumentNullException>(() => host.RunChunk("return 1", null));
            Assert.Throws<ArgumentException>(() => host.RunChunk("return 1", " "));
        }
    }
}
