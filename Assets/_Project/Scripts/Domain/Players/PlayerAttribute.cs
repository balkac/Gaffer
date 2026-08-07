namespace Gaffer.Domain.Players
{
    /// <summary>
    /// Names one number on an <see cref="Attributes"/> sheet. It exists so "which attributes constitute
    /// this role" can be stated once as data (<see cref="RoleAttributeWeights"/>) instead of being
    /// re-typed as a hand-written expression in the rating, again in the development curve, and again in
    /// the scout's display rows — the shape ARCHITECTURE §8a warns about, where the constraint is
    /// invisible at every individual call site and nothing fails when one copy drifts.
    /// <see cref="Attributes.ValueOf"/> / <see cref="Attributes.WithValue"/> are the generic reader and
    /// writer that turn a member of this enum back into a stat.
    /// </summary>
    /// <remarks>
    /// The numeric values are pinned for the same reason <see cref="Position"/>'s are: nothing writes this
    /// enum to a save or an <c>.asset</c> today, but it is the same class of classification data, and the
    /// moment it reaches one the ordinal *is* the stored value — pinning after the fact is too late. Never
    /// reorder, renumber, or reuse a value; only ever append.
    /// </remarks>
    public enum PlayerAttribute
    {
        // Technical
        Finishing = 0,
        Technique = 1,
        FirstTouch = 2,
        Dribbling = 3,
        Passing = 4,
        Crossing = 5,
        Heading = 6,
        LongShots = 7,
        Marking = 8,
        Tackling = 9,

        // Set-piece
        Penalties = 10,
        FreeKicks = 11,
        Corners = 12,
        LongThrows = 13,

        // Physical & Movement
        Pace = 14,
        Acceleration = 15,
        Stamina = 16,
        Strength = 17,
        Agility = 18,
        Jumping = 19,
        Balance = 20,
        Positioning = 21,

        // Goalkeeping
        Reflexes = 22,
        Handling = 23,
        AerialReach = 24,
        CommandOfArea = 25,
        OneOnOnes = 26,
        Kicking = 27,
        GkPositioning = 28,
    }
}
