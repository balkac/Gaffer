using System;

namespace Gaffer.Domain.Clubs
{
    /// <summary>A lightweight identity for a club — an Id, not an enum, so clubs live in data (NON-NEGOTIABLE).</summary>
    public readonly struct ClubId : IEquatable<ClubId>
    {
        public ClubId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(ClubId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is ClubId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(ClubId left, ClubId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClubId left, ClubId right)
        {
            return !left.Equals(right);
        }
    }
}
