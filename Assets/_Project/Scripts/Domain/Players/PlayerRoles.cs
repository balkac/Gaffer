namespace Gaffer.Domain.Players
{
    /// <summary>
    /// Maps a specific <see cref="PlayerRole"/> to the broad <see cref="Position"/> line the simulation
    /// reasons about, names the localization key for its short label, and names a representative role for a
    /// line. Wingers sit on the forward line, wide midfielders on the midfield line — the split that keeps a
    /// 4-4-2 and a 4-3-3 shaped differently in the strength derivation. What a role is *made of* is a
    /// separate table, <see cref="RoleAttributeWeights"/>.
    ///
    /// <para>It also owns the distance between roles — <see cref="FitFor"/>, how badly a player is out of
    /// position in a given slot. Role topology in one place: which line a role sits on and how far one line
    /// is from another are the same piece of knowledge, and answering the second somewhere else would mean
    /// a second, drifting opinion about the first.</para>
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
        /// How far up the pitch a line sits, 0 at the goalkeeper and 3 at the forwards — the topology the
        /// out-of-position classification measures distance on.
        ///
        /// <para><b>Stated, not inferred.</b> <see cref="Position"/>'s numeric values happen to run in this
        /// order, so <c>(int)line</c> would work today and would keep working right up until somebody adds
        /// a line (a sweeper, a wing-back tier) with the next free value and silently makes it the furthest
        /// forward thing on the pitch. The ordinals are a persistence contract, not a football fact.</para>
        /// </summary>
        public static int LineDepth(Position line)
        {
            switch (line)
            {
                case Position.Goalkeeper:
                    return 0;
                case Position.Defender:
                    return 1;
                case Position.Midfielder:
                    return 2;
                default:
                    return 3;
            }
        }

        /// <summary>
        /// How well a player's own role suits the slot he is being put in. The single definition of "out of
        /// position" in the game: the auto-picker ranks candidates through it, the match strength charges
        /// for it, and the squad screen marks it — one rule, so the mark on the screen and the cost on the
        /// pitch can never disagree (ARCHITECTURE §8a).
        /// </summary>
        public static PositionalFit FitFor(PlayerRole playerRole, PlayerRole slotRole)
        {
            if (playerRole == slotRole)
            {
                return PositionalFit.Natural;
            }

            Position his = Line(playerRole);
            Position wanted = Line(slotRole);
            if (his == wanted)
            {
                return PositionalFit.SameLine;
            }

            // A keeper is a different job, not a further one: the distance from goal to defence is 1, but
            // no outfielder keeps goal and no keeper plays out, so the pair is disqualified before the
            // distance is even measured.
            if (his == Position.Goalkeeper || wanted == Position.Goalkeeper)
            {
                return PositionalFit.Impossible;
            }

            int distance = LineDepth(wanted) - LineDepth(his);
            if (distance < 0)
            {
                distance = -distance;
            }

            return distance == 1 ? PositionalFit.AdjacentLine : PositionalFit.DistantLine;
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
