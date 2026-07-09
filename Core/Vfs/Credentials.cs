using System;
using System.Collections.Generic;

namespace Siegebox.Vfs
{
    public sealed class Credentials
    {
        private const int RootUid = 0;

        private readonly HashSet<int> groupIds;

        public int Uid { get; }

        public IReadOnlyCollection<int> Gids => groupIds;

        public bool IsRoot => Uid == RootUid;

        public Credentials(int uid, params int[] groupIds)
        {
            if (groupIds is null)
            {
                throw new ArgumentNullException(nameof(groupIds));
            }

            Uid = uid;
            this.groupIds = new HashSet<int>(groupIds);
        }

        public bool InGroup(int gid) => groupIds.Contains(gid);
    }
}
