namespace Gaffer.Domain.Players
{
    /// <summary>
    /// Maps a specific <see cref="PlayerRole"/> to the broad <see cref="Position"/> line the simulation
    /// reasons about, gives a short display label, and names a representative role for a line. Wingers sit
    /// on the forward line, wide midfielders on the midfield line — the split that keeps a 4-4-2 and a
    /// 4-3-3 shaped differently in the strength derivation.
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

        public static string Abbrev(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return "GK";
                case PlayerRole.RightBack:
                    return "RB";
                case PlayerRole.CentreBack:
                    return "CB";
                case PlayerRole.LeftBack:
                    return "LB";
                case PlayerRole.DefensiveMidfield:
                    return "DM";
                case PlayerRole.CentralMidfield:
                    return "CM";
                case PlayerRole.AttackingMidfield:
                    return "AM";
                case PlayerRole.RightMidfield:
                    return "RM";
                case PlayerRole.LeftMidfield:
                    return "LM";
                case PlayerRole.RightWing:
                    return "RW";
                case PlayerRole.LeftWing:
                    return "LW";
                default:
                    return "ST";
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
