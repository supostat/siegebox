using System;
using System.Collections.Generic;

namespace Siegebox.Shell
{
    public sealed class ExportBuiltin : IBuiltin
    {
        public string Name => "export";

        public BuiltinResult Execute(ShellSession session, IReadOnlyList<string> arguments)
        {
            if (session is null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (arguments is null)
            {
                throw new ArgumentNullException(nameof(arguments));
            }

            if (arguments.Count == 0)
            {
                return BuiltinResult.Completed(1, "", "export: usage: export NAME=VALUE\n");
            }

            foreach (var assignment in arguments)
            {
                var separator = assignment.IndexOf('=');
                if (separator <= 0)
                {
                    return BuiltinResult.Completed(1, "", $"export: {assignment}: not a valid assignment\n");
                }

                var name = assignment.Substring(0, separator);
                var value = assignment.Substring(separator + 1);
                session.Environment[name] = value;
            }

            return BuiltinResult.Completed(0);
        }
    }
}
