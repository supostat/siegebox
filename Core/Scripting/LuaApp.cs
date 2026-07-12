using System;
using MoonSharp.Interpreter;
using Siegebox.App;

namespace Siegebox.Scripting
{
    /// <summary>
    /// An app whose lifecycle hooks live in Lua. on_launch runs under the full budget and
    /// its errors propagate — a failed launch aborts opening the window. Focus and close
    /// hooks run small-budget and contained, reporting failures to the error sink. Nil
    /// hooks are skipped. Each instance owns its app table, so Text is per-instance.
    /// </summary>
    public sealed class LuaApp : IApp, ITextContentApp
    {
        private readonly LuaHost host;
        private readonly DynValue onLaunch;
        private readonly DynValue onFocusGained;
        private readonly DynValue onFocusLost;
        private readonly DynValue onClosed;
        private readonly Action<string> errorSink;
        private readonly DynValue appTable;

        internal LuaApp(
            string title,
            LuaHost host,
            DynValue onLaunch,
            DynValue onFocusGained,
            DynValue onFocusLost,
            DynValue onClosed,
            Action<string> errorSink)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            this.host = host ?? throw new ArgumentNullException(nameof(host));
            this.onLaunch = onLaunch ?? throw new ArgumentNullException(nameof(onLaunch));
            this.onFocusGained = onFocusGained ?? throw new ArgumentNullException(nameof(onFocusGained));
            this.onFocusLost = onFocusLost ?? throw new ArgumentNullException(nameof(onFocusLost));
            this.onClosed = onClosed ?? throw new ArgumentNullException(nameof(onClosed));
            this.errorSink = errorSink ?? throw new ArgumentNullException(nameof(errorSink));
            Text = string.Empty;
            appTable = BuildAppTable();
        }

        public string Title { get; }

        public string Text { get; private set; }

        public int Revision { get; private set; }

        public AppState State { get; private set; } = AppState.Created;

        public void OnLaunched()
        {
            if (State != AppState.Created)
            {
                throw new InvalidOperationException("An app instance launches once.");
            }

            State = AppState.Running;
            host.CallToCompletion(onLaunch, new[] { appTable }, $"{Title}.on_launch");
        }

        public void OnFocusGained() => RunContainedHook(onFocusGained, "on_focus");

        public void OnFocusLost() => RunContainedHook(onFocusLost, "on_focus_lost");

        public void OnClosed()
        {
            if (State == AppState.Closed)
            {
                return;
            }

            State = AppState.Closed;
            RunContainedHook(onClosed, "on_close");
        }

        private void RunContainedHook(DynValue hook, string hookName)
        {
            if (hook.IsNil())
            {
                return;
            }

            try
            {
                host.CallBounded(hook, new[] { appTable }, $"{Title}.{hookName}");
            }
            catch (Exception hookError)
            {
                errorSink($"{Title}: {hookName}: {hookError.Message}");
            }
        }

        private DynValue BuildAppTable()
        {
            var table = new Table(host.Script);
            table.Set("set_text", DynValue.NewCallback(SetText));
            return DynValue.NewTable(table);
        }

        private DynValue SetText(ScriptExecutionContext callbackContext, CallbackArguments callbackArguments)
        {
            Text = callbackArguments.AsType(0, "set_text", DataType.String, false).String;
            Revision++;
            return DynValue.Nil;
        }
    }
}
