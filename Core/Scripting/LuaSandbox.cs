using MoonSharp.Interpreter;

namespace Siegebox.Scripting
{
    /// <summary>
    /// Builds hard-sandboxed MoonSharp scripts: core table/string/math modules plus
    /// metatables and error handling, and nothing that reaches the host. Deliberately
    /// absent: load/loadstring/dofile/require, os, io, dynamic evaluation, json, and
    /// coroutine — so scripts cannot dodge the instruction budget.
    /// </summary>
    public static class LuaSandbox
    {
        public static Script CreateScript()
        {
            var script = new Script(CoreModules.Preset_HardSandbox | CoreModules.Metatables | CoreModules.ErrorHandling);
            script.Options.DebugPrint = _ => { };
            return script;
        }
    }
}
