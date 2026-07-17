using System;

namespace Gaffer.Domain.Traits
{
    /// <summary>
    /// A lightweight identity for a trait — an Id, not an enum, because the definition lives in data
    /// (NON-NEGOTIABLE #3): new traits are authored as assets and referenced by slug, so content can
    /// grow without touching code. String-backed so an asset's stable name is the identity.
    /// </summary>
    public readonly struct TraitId : IEquatable<TraitId>
    {
        public TraitId(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public bool Equals(TraitId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is TraitId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Value != null ? Value.GetHashCode() : 0;
        }

        public static bool operator ==(TraitId left, TraitId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(TraitId left, TraitId right)
        {
            return !left.Equals(right);
        }
    }
}
