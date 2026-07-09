using System;

namespace Siegebox.Vfs
{
    public readonly struct PermissionMode : IEquatable<PermissionMode>
    {
        private const int MinimumBits = 0;
        private const int MaximumBits = 511;
        private const int ClassMask = 0b111;
        private const int OwnerShift = 6;
        private const int GroupShift = 3;

        public const int ReadBit = 4;
        public const int WriteBit = 2;
        public const int ExecuteBit = 1;

        public int Bits { get; }

        public PermissionMode(int bits)
        {
            if (bits < MinimumBits || bits > MaximumBits)
            {
                throw new ArgumentOutOfRangeException(nameof(bits), bits, "Permission bits must be in the range 0..511.");
            }

            Bits = bits;
        }

        public int OwnerRwx => (Bits >> OwnerShift) & ClassMask;

        public int GroupRwx => (Bits >> GroupShift) & ClassMask;

        public int OtherRwx => Bits & ClassMask;

        public bool Equals(PermissionMode other) => Bits == other.Bits;

        public override bool Equals(object? obj) => obj is PermissionMode other && Equals(other);

        public override int GetHashCode() => Bits;

        public static bool operator ==(PermissionMode left, PermissionMode right) => left.Equals(right);

        public static bool operator !=(PermissionMode left, PermissionMode right) => !left.Equals(right);

        public override string ToString()
        {
            var characters = new char[9];
            WriteTriplet(characters, 0, OwnerRwx);
            WriteTriplet(characters, 3, GroupRwx);
            WriteTriplet(characters, 6, OtherRwx);
            return new string(characters);
        }

        private static void WriteTriplet(char[] target, int offset, int rwx)
        {
            target[offset] = (rwx & ReadBit) != 0 ? 'r' : '-';
            target[offset + 1] = (rwx & WriteBit) != 0 ? 'w' : '-';
            target[offset + 2] = (rwx & ExecuteBit) != 0 ? 'x' : '-';
        }
    }
}
