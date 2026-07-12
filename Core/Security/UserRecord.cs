namespace Siegebox.Security
{
    /// <summary>
    /// One parsed /etc/passwd entry: the public identity of a user (name, uid, primary gid,
    /// home). The password hash lives in /etc/shadow, not here — see <see cref="ShadowTable"/>.
    /// </summary>
    public sealed class UserRecord
    {
        public UserRecord(string name, int uid, int gid, string home)
        {
            Name = name;
            Uid = uid;
            Gid = gid;
            Home = home;
        }

        public string Name { get; }

        public int Uid { get; }

        public int Gid { get; }

        public string Home { get; }
    }
}
