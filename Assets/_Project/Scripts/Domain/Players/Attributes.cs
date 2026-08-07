using System;

namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A player's raw numeric stats (0–100) — the first of the four player layers (TDD §4.2 / §5). A
    /// grouped, FM-like set: Technical, Set-piece, Physical &amp; Movement, and Goalkeeping (meaningful
    /// only for keepers; ~0 for outfielders). The FM "mental" axis is deliberately absent — traits and
    /// personality (Layer 2) absorb it, so intuition and character are not double-counted. Which attributes
    /// a role is made of, and how much each counts, lives in <see cref="RoleAttributeWeights"/> — one table,
    /// not a rule repeated per caller. A value object with value equality: settable auto-properties let the
    /// generator build one by object initializer, and struct copy semantics keep a player's copy effectively
    /// immutable (allocates nothing on the sim hot path — PERFORMANCE §4).
    /// <para>
    /// <see cref="ValueOf"/> and <see cref="WithValue"/> are the generic accessors that let a caller work
    /// from a <see cref="PlayerAttribute"/> identity instead of naming a property, which is what lets the
    /// rating, the development curve and the scout all read one shared role table. Both are
    /// <c>readonly</c> members, so passing this struct as an <c>in</c> parameter reads it in place instead
    /// of forcing the compiler to take a defensive 32-byte copy.
    /// </para>
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

        /// <summary>
        /// Reads one attribute by identity. A jump table over the enum, not a delegate: a delegate taking
        /// <see cref="Attributes"/> would copy all 32 bytes per read, and the rating reads six of these
        /// roughly 60,000 times a season (PERFORMANCE §4).
        /// </summary>
        public readonly byte ValueOf(PlayerAttribute attribute)
        {
            switch (attribute)
            {
                case PlayerAttribute.Finishing:
                    return Finishing;
                case PlayerAttribute.Technique:
                    return Technique;
                case PlayerAttribute.FirstTouch:
                    return FirstTouch;
                case PlayerAttribute.Dribbling:
                    return Dribbling;
                case PlayerAttribute.Passing:
                    return Passing;
                case PlayerAttribute.Crossing:
                    return Crossing;
                case PlayerAttribute.Heading:
                    return Heading;
                case PlayerAttribute.LongShots:
                    return LongShots;
                case PlayerAttribute.Marking:
                    return Marking;
                case PlayerAttribute.Tackling:
                    return Tackling;
                case PlayerAttribute.Penalties:
                    return Penalties;
                case PlayerAttribute.FreeKicks:
                    return FreeKicks;
                case PlayerAttribute.Corners:
                    return Corners;
                case PlayerAttribute.LongThrows:
                    return LongThrows;
                case PlayerAttribute.Pace:
                    return Pace;
                case PlayerAttribute.Acceleration:
                    return Acceleration;
                case PlayerAttribute.Stamina:
                    return Stamina;
                case PlayerAttribute.Strength:
                    return Strength;
                case PlayerAttribute.Agility:
                    return Agility;
                case PlayerAttribute.Jumping:
                    return Jumping;
                case PlayerAttribute.Balance:
                    return Balance;
                case PlayerAttribute.Positioning:
                    return Positioning;
                case PlayerAttribute.Reflexes:
                    return Reflexes;
                case PlayerAttribute.Handling:
                    return Handling;
                case PlayerAttribute.AerialReach:
                    return AerialReach;
                case PlayerAttribute.CommandOfArea:
                    return CommandOfArea;
                case PlayerAttribute.OneOnOnes:
                    return OneOnOnes;
                case PlayerAttribute.Kicking:
                    return Kicking;
                case PlayerAttribute.GkPositioning:
                    return GkPositioning;
                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Unmapped player attribute.");
            }
        }

        /// <summary>
        /// The same sheet with one attribute replaced — the write side of <see cref="ValueOf"/>. Returning a
        /// copy rather than mutating in place keeps the struct's only mutation surface the object
        /// initializer, so callers stay honest about what they changed; the copy is a seasonal cost
        /// (development), never a per-match one.
        /// </summary>
        public readonly Attributes WithValue(PlayerAttribute attribute, byte value)
        {
            Attributes updated = this;
            switch (attribute)
            {
                case PlayerAttribute.Finishing:
                    updated.Finishing = value;
                    break;
                case PlayerAttribute.Technique:
                    updated.Technique = value;
                    break;
                case PlayerAttribute.FirstTouch:
                    updated.FirstTouch = value;
                    break;
                case PlayerAttribute.Dribbling:
                    updated.Dribbling = value;
                    break;
                case PlayerAttribute.Passing:
                    updated.Passing = value;
                    break;
                case PlayerAttribute.Crossing:
                    updated.Crossing = value;
                    break;
                case PlayerAttribute.Heading:
                    updated.Heading = value;
                    break;
                case PlayerAttribute.LongShots:
                    updated.LongShots = value;
                    break;
                case PlayerAttribute.Marking:
                    updated.Marking = value;
                    break;
                case PlayerAttribute.Tackling:
                    updated.Tackling = value;
                    break;
                case PlayerAttribute.Penalties:
                    updated.Penalties = value;
                    break;
                case PlayerAttribute.FreeKicks:
                    updated.FreeKicks = value;
                    break;
                case PlayerAttribute.Corners:
                    updated.Corners = value;
                    break;
                case PlayerAttribute.LongThrows:
                    updated.LongThrows = value;
                    break;
                case PlayerAttribute.Pace:
                    updated.Pace = value;
                    break;
                case PlayerAttribute.Acceleration:
                    updated.Acceleration = value;
                    break;
                case PlayerAttribute.Stamina:
                    updated.Stamina = value;
                    break;
                case PlayerAttribute.Strength:
                    updated.Strength = value;
                    break;
                case PlayerAttribute.Agility:
                    updated.Agility = value;
                    break;
                case PlayerAttribute.Jumping:
                    updated.Jumping = value;
                    break;
                case PlayerAttribute.Balance:
                    updated.Balance = value;
                    break;
                case PlayerAttribute.Positioning:
                    updated.Positioning = value;
                    break;
                case PlayerAttribute.Reflexes:
                    updated.Reflexes = value;
                    break;
                case PlayerAttribute.Handling:
                    updated.Handling = value;
                    break;
                case PlayerAttribute.AerialReach:
                    updated.AerialReach = value;
                    break;
                case PlayerAttribute.CommandOfArea:
                    updated.CommandOfArea = value;
                    break;
                case PlayerAttribute.OneOnOnes:
                    updated.OneOnOnes = value;
                    break;
                case PlayerAttribute.Kicking:
                    updated.Kicking = value;
                    break;
                case PlayerAttribute.GkPositioning:
                    updated.GkPositioning = value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(attribute), attribute, "Unmapped player attribute.");
            }

            return updated;
        }

        public readonly bool Equals(Attributes other)
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

        public readonly override bool Equals(object obj)
        {
            return obj is Attributes other && Equals(other);
        }

        public readonly override int GetHashCode()
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
