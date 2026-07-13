using System;
using System.Text;
using Siegebox.Vfs;

namespace Siegebox.Documentation
{
    /// <summary>
    /// The single authored source of stock manual pages. <see cref="RegisterInto"/> populates the
    /// in-memory catalog that feeds <c>--help</c> and the doc browser; <see cref="SeedPages"/>
    /// writes the full page bodies into the VFS under <c>/usr/share/man</c> so the <c>man</c>
    /// command reads them under the caller's identity like any other file.
    /// </summary>
    public static class ManualSeed
    {
        private static readonly Credentials Root = new Credentials(0);
        private static readonly PermissionMode DirectoryMode = new PermissionMode(0b111_101_101);
        private static readonly PermissionMode PageMode = new PermissionMode(0b110_100_100);

        private static readonly ManualEntry[] Entries =
        {
            new ManualEntry("cat", "text", "usage: cat [FILE]...",
                "Concatenate files to standard output.",
                "NAME\n    cat - concatenate files and print on the standard output\n\nSYNOPSIS\n    cat [FILE]...\n\nDESCRIPTION\n    Copies each FILE to standard output in order. With no FILE, or when a\n    FILE is -, reads from standard input. An unreadable file is reported\n    on standard error and the next file is tried.\n"),
            new ManualEntry("ls", "file system", "usage: ls [FILE]...",
                "List directory contents.",
                "NAME\n    ls - list directory contents\n\nSYNOPSIS\n    ls [FILE]...\n\nDESCRIPTION\n    Lists information about the FILEs, the current directory by default.\n    Directory arguments are listed with their entries; file and symlink\n    arguments are printed as themselves. Entries are sorted by name.\n"),
            new ManualEntry("echo", "text", "usage: echo [STRING]...",
                "Write arguments to standard output.",
                "NAME\n    echo - display a line of text\n\nSYNOPSIS\n    echo [STRING]...\n\nDESCRIPTION\n    Writes the STRING arguments to standard output, separated by single\n    spaces and followed by a newline.\n"),
            new ManualEntry("pwd", "file system", "usage: pwd",
                "Print the working directory.",
                "NAME\n    pwd - print name of current working directory\n\nSYNOPSIS\n    pwd\n\nDESCRIPTION\n    Prints the absolute path of the shell's current working directory.\n"),
            new ManualEntry("mkdir", "file system", "usage: mkdir DIRECTORY...",
                "Create directories.",
                "NAME\n    mkdir - make directories\n\nSYNOPSIS\n    mkdir DIRECTORY...\n\nDESCRIPTION\n    Creates each named DIRECTORY. It is an error if a directory already\n    exists or a parent component is missing.\n"),
            new ManualEntry("rm", "file system", "usage: rm FILE...",
                "Remove files or empty directories.",
                "NAME\n    rm - remove files or directories\n\nSYNOPSIS\n    rm FILE...\n\nDESCRIPTION\n    Removes each FILE. A directory must be empty to be removed. Each\n    failure is reported on standard error and the exit status is nonzero.\n"),
            new ManualEntry("mv", "file system", "usage: mv SOURCE DEST",
                "Move or rename a file.",
                "NAME\n    mv - move (rename) files\n\nSYNOPSIS\n    mv SOURCE DEST\n\nDESCRIPTION\n    Renames SOURCE to DEST. It is an error if DEST already exists.\n"),
            new ManualEntry("cp", "file system", "usage: cp SOURCE DEST",
                "Copy a file.",
                "NAME\n    cp - copy files\n\nSYNOPSIS\n    cp SOURCE DEST\n\nDESCRIPTION\n    Copies SOURCE to DEST, preserving its contents. It is an error if\n    DEST already exists.\n"),
            new ManualEntry("touch", "file system", "usage: touch FILE...",
                "Create empty files.",
                "NAME\n    touch - create empty files\n\nSYNOPSIS\n    touch FILE...\n\nDESCRIPTION\n    Creates each FILE that does not yet exist. An existing file is left\n    unchanged.\n"),
            new ManualEntry("chmod", "file system", "usage: chmod MODE FILE...",
                "Change file permission bits.",
                "NAME\n    chmod - change file mode bits\n\nSYNOPSIS\n    chmod MODE FILE...\n\nDESCRIPTION\n    Sets the permission bits of each FILE to the octal MODE. Only the\n    file's owner or root may change its mode.\n"),
            new ManualEntry("clear", "terminal", "usage: clear",
                "Clear the terminal screen.",
                "NAME\n    clear - clear the terminal screen\n\nSYNOPSIS\n    clear\n\nDESCRIPTION\n    Clears the scrollback and moves the cursor to the top-left by emitting\n    the terminal's clear escape sequence.\n"),
            new ManualEntry("help", "system", "usage: help",
                "List available builtins and commands.",
                "NAME\n    help - list available builtins and commands\n\nSYNOPSIS\n    help\n\nDESCRIPTION\n    Prints the names of every registered shell builtin and command. Use\n    'man PAGE' or 'COMMAND --help' for details on a single command.\n"),
            new ManualEntry("ps", "process", "usage: ps",
                "Report running processes.",
                "NAME\n    ps - report process status\n\nSYNOPSIS\n    ps\n\nDESCRIPTION\n    Lists the scheduler's processes with their state and name.\n"),
            new ManualEntry("kill", "process", "usage: kill PID...",
                "Signal processes by id.",
                "NAME\n    kill - terminate processes\n\nSYNOPSIS\n    kill PID...\n\nDESCRIPTION\n    Interrupts each process named by PID. An unprivileged user may only\n    signal its own processes; root may signal any.\n"),
            new ManualEntry("passwd", "system", "usage: passwd [USER]",
                "Change a user password.",
                "NAME\n    passwd - change a user's password\n\nSYNOPSIS\n    passwd [USER]\n\nDESCRIPTION\n    Changes the password for USER, or the current user when omitted. A\n    non-root user must supply the current password and may only change\n    their own.\n"),
            new ManualEntry("man", "system", "usage: man PAGE",
                "Read a manual page.",
                "NAME\n    man - read a manual page\n\nSYNOPSIS\n    man PAGE\n\nDESCRIPTION\n    Prints the manual page named PAGE, read from /usr/share/man under your\n    own identity. Use 'help' to list command names.\n"),
        };

        public static void RegisterInto(Manual manual)
        {
            if (manual is null)
            {
                throw new ArgumentNullException(nameof(manual));
            }

            foreach (var entry in Entries)
            {
                manual.Register(new ManualPage(entry.Name, entry.Category, entry.Synopsis, entry.Description));
            }
        }

        public static void SeedPages(VirtualFileSystem vfs)
        {
            if (vfs is null)
            {
                throw new ArgumentNullException(nameof(vfs));
            }

            vfs.CreateDirectory("/usr/share", DirectoryMode, Root);
            vfs.CreateDirectory("/usr/share/man", DirectoryMode, Root);
            foreach (var entry in Entries)
            {
                WritePage(vfs, entry.Name, entry.Body);
            }
        }

        private static void WritePage(VirtualFileSystem vfs, string name, string body)
        {
            var stream = vfs.OpenForWrite("/usr/share/man/" + name, WriteBehavior.Truncate, PageMode, Root);
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body);
                stream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                stream.CloseWrite();
            }
        }

        private sealed class ManualEntry
        {
            public ManualEntry(string name, string category, string synopsis, string description, string body)
            {
                Name = name;
                Category = category;
                Synopsis = synopsis;
                Description = description;
                Body = body;
            }

            public string Name { get; }

            public string Category { get; }

            public string Synopsis { get; }

            public string Description { get; }

            public string Body { get; }
        }
    }
}
