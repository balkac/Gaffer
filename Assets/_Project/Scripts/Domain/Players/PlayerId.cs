using System;

namespace Gaffer.Domain.Players
{
    /// <summary>A lightweight identity for a generated player — an Id, not an enum (NON-NEGOTIABLE).</summary>
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public PlayerId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(PlayerId other)
        {
            return Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value;
        }

        public static bool operator ==(PlayerId left, PlayerId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerId left, PlayerId right)
        {
            return !left.Equals(right);
        }
    }
}
