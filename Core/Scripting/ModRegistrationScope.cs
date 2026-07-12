using System;
using System.Collections.Generic;
using Siegebox.App;
using Siegebox.Events;
using Siegebox.Shell;

namespace Siegebox.Scripting
{
    /// <summary>
    /// Per-mod ledger of everything registered through the script api. Rollback undoes the
    /// registrations in reverse chronological order — disposing subscriptions, restoring
    /// overridden file type mappings, unregistering apps and commands — so a failed mod
    /// leaves the registries exactly as they were.
    /// </summary>
    public sealed class ModRegistrationScope
    {
        private readonly CommandRegistry commands;
        private readonly AppRegistry apps;
        private readonly FileTypeRegistry fileTypes;
        private readonly List<Action> rollbackActions = new List<Action>();

        internal ModRegistrationScope(CommandRegistry commands, AppRegistry apps, FileTypeRegistry fileTypes)
        {
            this.commands = commands;
            this.apps = apps;
            this.fileTypes = fileTypes;
        }

        internal void RecordCommand(string name) => rollbackActions.Add(() => commands.Unregister(name));

        internal void RecordApp(string id) => rollbackActions.Add(() => apps.Unregister(id));

        internal void RecordFileType(string extension, string? previousAppId)
            => rollbackActions.Add(() => RestoreFileType(extension, previousAppId));

        internal void RecordSubscription(EventSubscription subscription) => rollbackActions.Add(subscription.Dispose);

        public void Rollback()
        {
            for (var index = rollbackActions.Count - 1; index >= 0; index--)
            {
                rollbackActions[index]();
            }

            rollbackActions.Clear();
        }

        private void RestoreFileType(string extension, string? previousAppId)
        {
            if (previousAppId is null)
            {
                fileTypes.Unregister(extension);
            }
            else
            {
                fileTypes.Register(extension, previousAppId);
            }
        }
    }
}
