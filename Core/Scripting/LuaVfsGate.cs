using MoonSharp.Interpreter;

namespace Siegebox.Scripting
{
    /// <summary>
    /// A one-way latch guarding the privileged, install-identity vfs a mod's top-level
    /// chunk uses to seed files while it loads. It opens on install and the mod loader
    /// closes it once loading ends; a closed gate turns every vfs call — including one
    /// made through a reference the load chunk stashed in a closure — into a pcall-able
    /// error, so root vfs never survives into a command or app handler. It never carries
    /// per-process identity (that lives on each command's context), so closing it is not a
    /// race: the latch only ever goes open -&gt; closed, once.
    /// </summary>
    public sealed class LuaVfsGate
    {
        public bool IsOpen { get; private set; } = true;

        public void Close() => IsOpen = false;

        internal void EnsureOpen()
        {
            if (!IsOpen)
            {
                throw new ScriptRuntimeException("vfs is only available while a mod loads");
            }
        }
    }
}
