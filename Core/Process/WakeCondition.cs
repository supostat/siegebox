using System;
using Siegebox.Vfs;

namespace Siegebox.Process
{
    /// <summary>
    /// Describes what must happen before a sleeping process is stepped again.
    /// <c>default(WakeCondition)</c> equals <see cref="None"/> and never wakes a process.
    /// </summary>
    public readonly struct WakeCondition
    {
        private WakeCondition(WakeConditionKind kind, IByteStream? stream, int pid)
        {
            Kind = kind;
            Stream = stream;
            Pid = pid;
        }

        public static WakeCondition None => default;

        public WakeConditionKind Kind { get; }

        public IByteStream? Stream { get; }

        public int Pid { get; }

        /// <summary>
        /// Wakes when <paramref name="stream"/> has buffered bytes or has reached end of stream.
        /// The stream must be open for reading: probing a stream that rejects reads throws
        /// <see cref="InvalidOperationException"/>, which propagates out of <see cref="Scheduler.Tick"/>
        /// while the scheduler itself stays usable.
        /// </summary>
        public static WakeCondition Readable(IByteStream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return new WakeCondition(WakeConditionKind.StreamReadable, stream, 0);
        }

        /// <summary>
        /// Wakes when <paramref name="stream"/> has free capacity or has reached end of stream.
        /// The stream must be open for writing: probing a stream that rejects writes throws
        /// <see cref="InvalidOperationException"/>, which propagates out of <see cref="Scheduler.Tick"/>
        /// while the scheduler itself stays usable.
        /// </summary>
        public static WakeCondition Writable(IByteStream stream)
        {
            if (stream is null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            return new WakeCondition(WakeConditionKind.StreamWritable, stream, 0);
        }

        /// <summary>
        /// Wakes when the process with <paramref name="pid"/> has finished.
        /// <paramref name="pid"/> must be positive; a non-positive pid throws
        /// <see cref="ArgumentOutOfRangeException"/>.
        /// Waiting on a pid that never existed or was already reaped wakes immediately.
        /// </summary>
        public static WakeCondition ProcessExit(int pid)
        {
            if (pid <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pid), pid, "Pid must be positive.");
            }

            return new WakeCondition(WakeConditionKind.ProcessExit, null, pid);
        }
    }
}
