namespace Gaffer.Domain.Players
{
    /// <summary>A player's broad role. A fixed, small sim-level classification, so a plain enum.</summary>
    /// <remarks>
    /// The numeric values are pinned as a PERSISTENCE CONTRACT. Position is not itself written to a save
    /// today (it is derived from <see cref="PlayerRole"/> via <c>PlayerRoles.Line</c>), but it is the same
    /// class of classification data: the moment it reaches a save or a <c>.asset</c>, the ordinal *is* the
    /// stored value, and pinning after the fact is too late. Never reorder, renumber, or reuse a value.
    /// </remarks>
    public enum Position
    {
        Goalkeeper = 0,
        Defender = 1,
        Midfielder = 2,
        Forward = 3,
    }
}
