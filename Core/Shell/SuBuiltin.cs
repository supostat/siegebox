using System;
using System.Collections.Generic;
using Siegebox.Vfs;

namespace Siegebox.Shell
{
    /// <summary>
    /// Unauthenticated numeric identity switch: <c>su [uid [gid…]]</c>, default uid 0.
    /// A user database and authentication are out of scope for this phase by contract.
    /// </summary>
    public sealed class SuBuiltin : IBuiltin
    {
        public string Name => "su";

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

            var uid = 0;
            if (arguments.Count > 0 && !int.TryParse(arguments[0], out uid))
            {
                return BuiltinResult.Completed(1, "", $"su: {arguments[0]}: numeric uid expected\n");
            }

            var groupIds = new int[arguments.Count > 1 ? arguments.Count - 1 : 0];
            for (var index = 1; index < arguments.Count; index++)
            {
                if (!int.TryParse(arguments[index], out groupIds[index - 1]))
                {
                    return BuiltinResult.Completed(1, "", $"su: {arguments[index]}: numeric gid expected\n");
                }
            }

            session.Credentials = new Credentials(uid, groupIds);
            return BuiltinResult.Completed(0);
        }
    }
}
