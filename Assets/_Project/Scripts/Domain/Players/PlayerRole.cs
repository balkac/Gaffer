namespace Gaffer.Domain.Players
{
    /// <summary>
    /// A player's specific position — finer than the broad <see cref="Position"/> line, so a formation can
    /// field the right shape (a 3-5-2's three centre-backs, a 4-3-3's wingers) and the lineup picker slots
    /// the right player. The broad line each role belongs to (for the sim's strength axes) comes from
    /// <see cref="PlayerRoles.Line"/>. A fixed, small sim-level classification, so a plain enum.
    /// </summary>
    public enum PlayerRole
    {
        Goalkeeper,
        RightBack,
        CentreBack,
        LeftBack,
        DefensiveMidfield,
        CentralMidfield,
        AttackingMidfield,
        RightMidfield,
        LeftMidfield,
        RightWing,
        LeftWing,
        Striker,
    }
}
