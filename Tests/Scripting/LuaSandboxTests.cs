using NUnit.Framework;
using Siegebox.Scripting;

namespace Siegebox.Scripting.Tests
{
    [TestFixture]
    public sealed class LuaSandboxTests
    {
        [Test]
        public void Table_string_and_math_chunks_run()
        {
            var host = new LuaHost();

            var result = host.RunChunk(
                "local t = {3, 1, 2} table.sort(t) return t[1] .. string.upper('a') .. math.floor(2.9)",
                "happy");

            Assert.That(result.String, Is.EqualTo("1A2"));
        }

        [Test]
        public void Host_reaching_facilities_are_absent()
        {
            var host = new LuaHost();

            var result = host.RunChunk(
                "return os == nil and io == nil and require == nil and dofile == nil" +
                " and load == nil and loadstring == nil and coroutine == nil" +
                " and dynamic == nil and json == nil and debug == nil",
                "sandbox");

            Assert.That(result.Boolean, Is.True);
        }

        [Test]
        public void Print_is_silenced_and_the_chunk_keeps_running()
        {
            var host = new LuaHost();

            var result = host.RunChunk("print('never seen') return 42", "printprobe");

            Assert.That(result.Number, Is.EqualTo(42));
        }

        [Test]
        public void Infinite_loop_exhausts_the_budget_and_the_host_runs_the_next_chunk()
        {
            var host = new LuaHost();

            var error = Assert.Throws<LuaBudgetExceededException>(() => host.RunChunk("while true do end", "spin"));

            Assert.That(error.ChunkName, Is.EqualTo("spin"));
            Assert.That(host.RunChunk("return 41 + 1", "next").Number, Is.EqualTo(42));
        }
    }
}
