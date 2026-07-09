using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Rates a player on his specific role from his attributes — the shared scoring both the strength builder
    /// (to average a line) and the lineup selector (to pick the best per slot) rely on. Each role weights its
    /// own key attributes (see <see cref="RoleKeyAttributes"/>) with weights summing to 1, so the rating stays
    /// on the 0–100 scale (uniform attributes collapse to that value, whatever the role). The split is the
    /// point: a full-back is valued on pace and crossing, a centre-back on marking and heading, a winger on
    /// pace and dribbling — so the same attribute sheet is worth different amounts in different roles, and a
    /// player slotted out of position rates below a natural fit. The weights tune into a BalanceSO later.
    /// </summary>
    public static class PlayerRatings
    {
        public static double ForRole(PlayerRole role, Attributes a)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return (0.30 * a.Reflexes) + (0.20 * a.Handling) + (0.20 * a.OneOnOnes) + (0.15 * a.CommandOfArea) + (0.10 * a.AerialReach) + (0.05 * a.GkPositioning);
                case PlayerRole.CentreBack:
                    return (0.25 * a.Marking) + (0.25 * a.Tackling) + (0.20 * a.Heading) + (0.15 * a.Strength) + (0.15 * a.Positioning);
                case PlayerRole.RightBack:
                case PlayerRole.LeftBack:
                    return (0.20 * a.Pace) + (0.20 * a.Crossing) + (0.20 * a.Tackling) + (0.15 * a.Marking) + (0.15 * a.Stamina) + (0.10 * a.Positioning);
                case PlayerRole.DefensiveMidfield:
                    return (0.25 * a.Tackling) + (0.20 * a.Marking) + (0.20 * a.Positioning) + (0.20 * a.Passing) + (0.15 * a.Stamina);
                case PlayerRole.CentralMidfield:
                    return (0.30 * a.Passing) + (0.25 * a.Technique) + (0.15 * a.FirstTouch) + (0.15 * a.Positioning) + (0.15 * a.Stamina);
                case PlayerRole.AttackingMidfield:
                    return (0.25 * a.Passing) + (0.25 * a.Technique) + (0.20 * a.Dribbling) + (0.15 * a.FirstTouch) + (0.15 * a.LongShots);
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    return (0.25 * a.Crossing) + (0.20 * a.Pace) + (0.20 * a.Stamina) + (0.20 * a.Passing) + (0.15 * a.Dribbling);
                case PlayerRole.RightWing:
                case PlayerRole.LeftWing:
                    return (0.25 * a.Pace) + (0.25 * a.Dribbling) + (0.20 * a.Crossing) + (0.15 * a.Technique) + (0.15 * a.Finishing);
                default: // Striker
                    return (0.35 * a.Finishing) + (0.20 * a.Positioning) + (0.20 * a.Pace) + (0.15 * a.Technique) + (0.10 * a.Heading);
            }
        }

        /// <summary>How good a player is in his own role — the rating on the axis matching his specific role.</summary>
        public static double ForRole(Player player)
        {
            return ForRole(player.Role, player.Attributes);
        }
    }
}
