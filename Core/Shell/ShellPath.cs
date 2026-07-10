namespace Siegebox.Shell
{
    /// <summary>The VFS is cwd-blind: joining a relative path onto the session cwd happens here.</summary>
    internal static class ShellPath
    {
        public static string Absolute(string workingDirectory, string path)
        {
            if (path.Length > 0 && path[0] == '/')
            {
                return path;
            }

            return workingDirectory == "/" ? "/" + path : workingDirectory + "/" + path;
        }
    }
}
