namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A player's specific position — finer than the broad <see cref="Position"/> line, so a formation can
    /// field the right shape (a 3-5-2's three centre-backs, a 4-3-3's wingers) and the lineup picker slots
    /// the right player. The broad line each role belongs to (for the sim's strength axes) comes from
    /// <see cref="PlayerRoles.Line"/>. A fixed, small sim-level classification, so a plain enum.
    /// </summary>
    /// <remarks>
    /// The numeric values are a PERSISTENCE CONTRACT, not decoration. Saves before schema v5 stored the
    /// role as its ordinal, so those numbers are wire format: never reorder, renumber, or reuse a value,
    /// and only ever append. The v4 → v5 migration (<c>SaveMigrator</c>) can only read an old save
    /// *because* these values are pinned. From v5 on the save carries the member NAME (CONVENTIONS §6,
    /// UNITY.md §7), which makes the names wire format too — a rename needs a migration step.
    /// <c>PersistedEnumValueTests</c> pins every member so a reorder fails loudly, by name.
    /// </remarks>
    public enum PlayerRole
    {
        Goalkeeper = 0,
        RightBack = 1,
        CentreBack = 2,
        LeftBack = 3,
        DefensiveMidfield = 4,
        CentralMidfield = 5,
        AttackingMidfield = 6,
        RightMidfield = 7,
        LeftMidfield = 8,
        RightWing = 9,
        LeftWing = 10,
        Striker = 11,
    }
}
