using System;

namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// A lightweight identity for a drama event — an Id, not an enum, because events are content that
    /// lives in data (NON-NEGOTIABLE #3): new drama = a new authored asset referenced by slug.
    /// </summary>
    public readonly struct DramaEventId : IEquatable<DramaEventId>
    {
        public DramaEventId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public bool Equals(DramaEventId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is DramaEventId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public static bool operator ==(DramaEventId left, DramaEventId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DramaEventId left, DramaEventId right)
        {
            return !left.Equals(right);
        }
    }
}
