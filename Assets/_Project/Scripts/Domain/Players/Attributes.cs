using System;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A player's raw numeric stats — the first of the four player layers (TDD §4.2 / §5). A packed
    /// value object: a <c>readonly struct</c> with value equality so it allocates nothing on the sim
    /// hot path (PERFORMANCE §4). The set is deliberately narrow (TDD §4.2). Stats are trusted as
    /// internally valid (CONVENTIONS §4) — the generator produces them in range.
    /// </summary>
    public readonly struct Attributes : IEquatable<Attributes>
    {
        public Attributes(byte pace, byte finishing, byte passing, byte tackling, byte positioning, byte stamina)
        {
            Pace = pace;
            Finishing = finishing;
            Passing = passing;
            Tackling = tackling;
            Positioning = positioning;
            Stamina = stamina;
        }

        public byte Pace { get; }

        public byte Finishing { get; }

        public byte Passing { get; }

        public byte Tackling { get; }

        public byte Positioning { get; }

        public byte Stamina { get; }

        public bool Equals(Attributes other)
        {
            return Pace == other.Pace
                && Finishing == other.Finishing
                && Passing == other.Passing
                && Tackling == other.Tackling
                && Positioning == other.Positioning
                && Stamina == other.Stamina;
        }

        public override bool Equals(object obj)
        {
            return obj is Attributes other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + Pace;
                hash = (hash * 31) + Finishing;
                hash = (hash * 31) + Passing;
                hash = (hash * 31) + Tackling;
                hash = (hash * 31) + Positioning;
                hash = (hash * 31) + Stamina;
                return hash;
            }
        }

        public static bool operator ==(Attributes left, Attributes right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Attributes left, Attributes right)
        {
            return !left.Equals(right);
        }
    }
}
