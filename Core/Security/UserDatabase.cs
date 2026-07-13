using System;
using System.Collections.Generic;

namespace Siegebox.Security
{
    /// <summary>
    /// Parses /etc/passwd content — one <c>name:uid:gid:home</c> entry per line — into
    /// looked-up <see cref="UserRecord"/>s. Blank lines and <c>#</c> comments are ignored;
    /// any other malformed line is rejected so a corrupt db never silently loses a user.
    /// Engine-free, no VFS: the reader (<see cref="AuthenticationService"/>) supplies the text.
    /// </summary>
    public sealed class UserDatabase
    {
        private const int FieldCount = 4;

        private readonly List<UserRecord> records;

        private UserDatabase(List<UserRecord> records)
        {
            this.records = records;
        }

        public IReadOnlyList<UserRecord> Records => records;

        public static UserDatabase Parse(string passwdContent)
        {
            if (passwdContent is null)
            {
                throw new ArgumentNullException(nameof(passwdContent));
            }

            var records = new List<UserRecord>();
            var lineNumber = 0;
            foreach (var rawLine in passwdContent.Split('\n'))
            {
                lineNumber++;
                var line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                records.Add(ParseLine(line, lineNumber));
            }

            return new UserDatabase(records);
        }

        public bool TryGetByName(string name, out UserRecord record)
        {
            foreach (var candidate in records)
            {
                if (candidate.Name == name)
                {
                    record = candidate;
                    return true;
                }
            }

            record = null!;
            return false;
        }

        public bool TryGetByUid(int uid, out UserRecord record)
        {
            foreach (var candidate in records)
            {
                if (candidate.Uid == uid)
                {
                    record = candidate;
                    return true;
                }
            }

            record = null!;
            return false;
        }

        private static UserRecord ParseLine(string line, int lineNumber)
        {
            var fields = line.Split(':');
            if (fields.Length != FieldCount)
            {
                throw new UserDatabaseException($"passwd: malformed entry on line {lineNumber}");
            }

            var name = fields[0];
            if (name.Length == 0)
            {
                throw new UserDatabaseException($"passwd: blank user name on line {lineNumber}");
            }

            if (!int.TryParse(fields[1], out var uid) || uid < 0)
            {
                throw new UserDatabaseException($"passwd: invalid uid on line {lineNumber}");
            }

            if (!int.TryParse(fields[2], out var gid) || gid < 0)
            {
                throw new UserDatabaseException($"passwd: invalid gid on line {lineNumber}");
            }

            var home = fields[3];
            if (home.Length == 0 || home[0] != '/')
            {
                throw new UserDatabaseException($"passwd: invalid home on line {lineNumber}");
            }

            return new UserRecord(name, uid, gid, home);
        }
    }
}
