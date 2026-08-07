namespace Gaffer.Domain.Players
{
    /// <summary>
    /// Maps a specific <see cref="PlayerRole"/> to the broad <see cref="Position"/> line the simulation
    /// reasons about, names the localization key for its short label, and names a representative role for a
    /// line. Wingers sit on the forward line, wide midfielders on the midfield line — the split that keeps a
    /// 4-4-2 and a 4-3-3 shaped differently in the strength derivation. What a role is *made of* is a
    /// separate table, <see cref="RoleAttributeWeights"/>.
    /// </summary>
    public static class PlayerRoles
    {
        public static Position Line(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return Position.Goalkeeper;
                case PlayerRole.RightBack:
                case PlayerRole.CentreBack:
                case PlayerRole.LeftBack:
                    return Position.Defender;
                case PlayerRole.DefensiveMidfield:
                case PlayerRole.CentralMidfield:
                case PlayerRole.AttackingMidfield:
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    return Position.Midfielder;
                default:
                    return Position.Forward;
            }
        }

        /// <summary>
        /// The localization key for the role's short label — the string table turns it into "GK", "CB", "ST"
        /// in English and into whatever Turkish uses. Domain names the role and hands out a key; it never
        /// carries display text (NON-NEGOTIABLE #8).
        /// </summary>
        public static string GetShortLabelKey(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return "role.goalkeeper.abbrev";
                case PlayerRole.RightBack:
                    return "role.right_back.abbrev";
                case PlayerRole.CentreBack:
                    return "role.centre_back.abbrev";
                case PlayerRole.LeftBack:
                    return "role.left_back.abbrev";
                case PlayerRole.DefensiveMidfield:
                    return "role.defensive_midfield.abbrev";
                case PlayerRole.CentralMidfield:
                    return "role.central_midfield.abbrev";
                case PlayerRole.AttackingMidfield:
                    return "role.attacking_midfield.abbrev";
                case PlayerRole.RightMidfield:
                    return "role.right_midfield.abbrev";
                case PlayerRole.LeftMidfield:
                    return "role.left_midfield.abbrev";
                case PlayerRole.RightWing:
                    return "role.right_wing.abbrev";
                case PlayerRole.LeftWing:
                    return "role.left_wing.abbrev";
                default:
                    return "role.striker.abbrev";
            }
        }

        /// <summary>A stand-in specific role for a broad line — used when only the line is known.</summary>
        public static PlayerRole Representative(Position line)
        {
            switch (line)
            {
                case Position.Goalkeeper:
                    return PlayerRole.Goalkeeper;
                case Position.Defender:
                    return PlayerRole.CentreBack;
                case Position.Midfielder:
                    return PlayerRole.CentralMidfield;
                default:
                    return PlayerRole.Striker;
            }
        }
    }
}
