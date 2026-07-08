using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Rates a player on each role axis from his attributes — the shared scoring both the strength builder
    /// (to average a line) and the lineup selector (to pick the best per position) rely on. Each rating
    /// weights a role's key attributes (see <see cref="RoleKeyAttributes"/>) with weights summing to 1, so
    /// it stays on the 0–100 scale. Defence branches on the role: a keeper is rated on the keeping group,
    /// an outfielder on tackling, marking, and heading. The weights tune into a BalanceSO later.
    /// </summary>
    public static class PlayerRatings
    {
        public static double Attack(Attributes a)
        {
            return (0.35 * a.Finishing) + (0.20 * a.Pace) + (0.20 * a.Technique) + (0.15 * a.Positioning) + (0.10 * a.Dribbling);
        }

        public static double Midfield(Attributes a)
        {
            return (0.30 * a.Passing) + (0.25 * a.Technique) + (0.15 * a.FirstTouch) + (0.15 * a.Positioning) + (0.15 * a.Stamina);
        }

        public static double Defence(Position position, Attributes a)
        {
            if (position == Position.Goalkeeper)
            {
                return (0.30 * a.Reflexes) + (0.20 * a.Handling) + (0.20 * a.OneOnOnes) + (0.15 * a.CommandOfArea) + (0.10 * a.AerialReach) + (0.05 * a.GkPositioning);
            }

            return (0.30 * a.Tackling) + (0.30 * a.Marking) + (0.15 * a.Heading) + (0.15 * a.Strength) + (0.10 * a.Positioning);
        }

        /// <summary>How good a player is in his own role — the axis matching his position.</summary>
        public static double ForPosition(Player player)
        {
            switch (player.Position)
            {
                case Position.Forward:
                    return Attack(player.Attributes);
                case Position.Midfielder:
                    return Midfield(player.Attributes);
                default:
                    return Defence(player.Position, player.Attributes);
            }
        }
    }
}
