using System;

namespace Siegebox.Events
{
    /// <summary>
    /// Flat immutable payload of one kernel hook. <see cref="ProcessSpawned"/> carries
    /// Pid/ProcessName/Uid, <see cref="ProcessExited"/> carries Pid/ProcessName/ExitCode,
    /// <see cref="FileOpened"/> carries Path/Uid/Access. Fields a factory does not set
    /// stay 0 or empty.
    /// </summary>
    public sealed class KernelEvent
    {
        public const string ProcessSpawnedName = "process.spawned";
        public const string ProcessExitedName = "process.exited";
        public const string FileOpenedName = "file.opened";

        private KernelEvent(string name, int pid, string processName, int exitCode, string path, int uid, string access)
        {
            Name = name;
            Pid = pid;
            ProcessName = processName;
            ExitCode = exitCode;
            Path = path;
            Uid = uid;
            Access = access;
        }

        public string Name { get; }

        public int Pid { get; }

        public string ProcessName { get; }

        public int ExitCode { get; }

        public string Path { get; }

        public int Uid { get; }

        public string Access { get; }

        public static KernelEvent ProcessSpawned(int pid, string processName, int uid)
        {
            if (processName is null)
            {
                throw new ArgumentNullException(nameof(processName));
            }

            return new KernelEvent(ProcessSpawnedName, pid, processName, 0, string.Empty, uid, string.Empty);
        }

        public static KernelEvent ProcessExited(int pid, string processName, int exitCode)
        {
            if (processName is null)
            {
                throw new ArgumentNullException(nameof(processName));
            }

            return new KernelEvent(ProcessExitedName, pid, processName, exitCode, string.Empty, 0, string.Empty);
        }

        public static KernelEvent FileOpened(string path, int uid, string access)
        {
            if (path is null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            if (access is null)
            {
                throw new ArgumentNullException(nameof(access));
            }

            return new KernelEvent(FileOpenedName, 0, string.Empty, 0, path, uid, access);
        }
    }
}
