using Gaffer.Domain.Players;

namespace Gaffer.Presentation.Squad
{
    /// <summary>
    /// How deep a role stands on the pitch. Six bands from the goal forward, which is what turns a
    /// formation's flat list of eleven roles into a shape somebody recognises.
    ///
    /// <para>Six rather than four, and that is the whole reason this type exists: a holding midfielder and
    /// a number ten are both "Midfielder" to the Domain, and drawing them on the same line makes 3-5-2 and
    /// 4-4-2 look like the same formation. The Domain's <c>Position</c> is about what a player IS, and
    /// this is about where he STANDS — a presentation question, kept out of the core.</para>
    /// </summary>
    public enum PitchBand
    {
        Goal = 0,
        Defence = 1,
        HoldingMidfield = 2,
        Midfield = 3,
        AttackingMidfield = 4,
        Attack = 5,
    }

    /// <summary>Reads a role into the band it stands in, and a slot into where it sits across the width.</summary>
    public static class PitchBands
    {
        /// <summary>The band this role plays in.</summary>
        public static PitchBand Of(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return PitchBand.Goal;
                case PlayerRole.RightBack:
                case PlayerRole.CentreBack:
                case PlayerRole.LeftBack:
                    return PitchBand.Defence;
                case PlayerRole.DefensiveMidfield:
                    return PitchBand.HoldingMidfield;
                case PlayerRole.RightMidfield:
                case PlayerRole.CentralMidfield:
                case PlayerRole.LeftMidfield:
                    return PitchBand.Midfield;
                case PlayerRole.AttackingMidfield:
                    return PitchBand.AttackingMidfield;
                case PlayerRole.RightWing:
                case PlayerRole.LeftWing:
                case PlayerRole.Striker:
                    return PitchBand.Attack;
                default:
                    // A role added tomorrow stands in midfield rather than vanishing off the pitch: a slot
                    // with nowhere to be would silently not draw, and eleven players would become ten
                    // (CONVENTIONS §1).
                    return PitchBand.Midfield;
            }
        }

        /// <summary>
        /// How far across the pitch a role sits, from 0 on the right touchline to 2 on the left. Used only
        /// to ORDER a band's slots, never to place them — spacing is the layout's business, so a band of
        /// two and a band of five both fill the width.
        /// </summary>
        public static int WidthOrderOf(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.RightBack:
                case PlayerRole.RightMidfield:
                case PlayerRole.RightWing:
                    return 0;
                case PlayerRole.LeftBack:
                case PlayerRole.LeftMidfield:
                case PlayerRole.LeftWing:
                    return 2;
                default:
                    return 1;
            }
        }
    }
}
