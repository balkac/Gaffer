using System;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A player's raw numeric stats (0–100) — the first of the four player layers (TDD §4.2 / §5). A
    /// grouped, FM-like set: Technical, Set-piece, Physical &amp; Movement, and Goalkeeping (meaningful
    /// only for keepers; ~0 for outfielders). The FM "mental" axis is deliberately absent — traits and
    /// personality (Layer 2) absorb it, so intuition and character are not double-counted. Each role
    /// emphasises its own key attributes (a display rule, see <see cref="RoleKeyAttributes"/>), not a
    /// separate value. A value object with value equality: settable auto-properties let the generator
    /// build one by object initializer, and struct copy semantics keep a player's copy effectively
    /// immutable (allocates nothing on the sim hot path — PERFORMANCE §4).
    /// </summary>
    public struct Attributes : IEquatable<Attributes>
    {
        // Technical
        public byte Finishing { get; set; }
        public byte Technique { get; set; }
        public byte FirstTouch { get; set; }
        public byte Dribbling { get; set; }
        public byte Passing { get; set; }
        public byte Crossing { get; set; }
        public byte Heading { get; set; }
        public byte LongShots { get; set; }
        public byte Marking { get; set; }
        public byte Tackling { get; set; }

        // Set-piece
        public byte Penalties { get; set; }
        public byte FreeKicks { get; set; }
        public byte Corners { get; set; }
        public byte LongThrows { get; set; }

        // Physical & Movement
        public byte Pace { get; set; }
        public byte Acceleration { get; set; }
        public byte Stamina { get; set; }
        public byte Strength { get; set; }
        public byte Agility { get; set; }
        public byte Jumping { get; set; }
        public byte Balance { get; set; }
        public byte Positioning { get; set; }

        // Goalkeeping (only meaningful for keepers; outfielders sit near 0)
        public byte Reflexes { get; set; }
        public byte Handling { get; set; }
        public byte AerialReach { get; set; }
        public byte CommandOfArea { get; set; }
        public byte OneOnOnes { get; set; }
        public byte Kicking { get; set; }
        public byte GkPositioning { get; set; }

        public bool Equals(Attributes other)
        {
            return Finishing == other.Finishing
                && Technique == other.Technique
                && FirstTouch == other.FirstTouch
                && Dribbling == other.Dribbling
                && Passing == other.Passing
                && Crossing == other.Crossing
                && Heading == other.Heading
                && LongShots == other.LongShots
                && Marking == other.Marking
                && Tackling == other.Tackling
                && Penalties == other.Penalties
                && FreeKicks == other.FreeKicks
                && Corners == other.Corners
                && LongThrows == other.LongThrows
                && Pace == other.Pace
                && Acceleration == other.Acceleration
                && Stamina == other.Stamina
                && Strength == other.Strength
                && Agility == other.Agility
                && Jumping == other.Jumping
                && Balance == other.Balance
                && Positioning == other.Positioning
                && Reflexes == other.Reflexes
                && Handling == other.Handling
                && AerialReach == other.AerialReach
                && CommandOfArea == other.CommandOfArea
                && OneOnOnes == other.OneOnOnes
                && Kicking == other.Kicking
                && GkPositioning == other.GkPositioning;
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
                hash = (hash * 31) + Finishing;
                hash = (hash * 31) + Technique;
                hash = (hash * 31) + FirstTouch;
                hash = (hash * 31) + Dribbling;
                hash = (hash * 31) + Passing;
                hash = (hash * 31) + Crossing;
                hash = (hash * 31) + Heading;
                hash = (hash * 31) + LongShots;
                hash = (hash * 31) + Marking;
                hash = (hash * 31) + Tackling;
                hash = (hash * 31) + Penalties;
                hash = (hash * 31) + FreeKicks;
                hash = (hash * 31) + Corners;
                hash = (hash * 31) + LongThrows;
                hash = (hash * 31) + Pace;
                hash = (hash * 31) + Acceleration;
                hash = (hash * 31) + Stamina;
                hash = (hash * 31) + Strength;
                hash = (hash * 31) + Agility;
                hash = (hash * 31) + Jumping;
                hash = (hash * 31) + Balance;
                hash = (hash * 31) + Positioning;
                hash = (hash * 31) + Reflexes;
                hash = (hash * 31) + Handling;
                hash = (hash * 31) + AerialReach;
                hash = (hash * 31) + CommandOfArea;
                hash = (hash * 31) + OneOnOnes;
                hash = (hash * 31) + Kicking;
                hash = (hash * 31) + GkPositioning;
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
