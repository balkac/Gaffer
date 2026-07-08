using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Picks the strongest starting eleven from a squad for a formation: the best keeper, then the best
    /// defenders, midfielders, and forwards to fill each line by <see cref="PlayerRatings.ForPosition"/>.
    /// If a line is short of bodies it is topped up from the best players left over, so the eleven is
    /// always filled (up to the squad size). Deterministic — ties break on the lower player id — so it is
    /// the natural default a manager then adjusts, and the auto-pick for AI clubs.
    /// </summary>
    public sealed class LineupSelector
    {
        public IReadOnlyList<Player> SelectBest(Squad squad, Formation formation)
        {
            List<Player> keepers = SortedByRating(squad, Position.Goalkeeper);
            List<Player> defenders = SortedByRating(squad, Position.Defender);
            List<Player> midfielders = SortedByRating(squad, Position.Midfielder);
            List<Player> forwards = SortedByRating(squad, Position.Forward);

            var chosen = new List<Player>(formation.Total);
            var used = new HashSet<int>();
            Take(chosen, used, keepers, 1);
            Take(chosen, used, defenders, formation.Defenders);
            Take(chosen, used, midfielders, formation.Midfielders);
            Take(chosen, used, forwards, formation.Forwards);

            if (chosen.Count < formation.Total)
            {
                List<Player> everyone = SortedByRating(squad, null);
                foreach (Player player in everyone)
                {
                    if (chosen.Count >= formation.Total)
                    {
                        break;
                    }

                    if (used.Add(player.Id.Value))
                    {
                        chosen.Add(player);
                    }
                }
            }

            return chosen;
        }

        private static void Take(List<Player> chosen, HashSet<int> used, List<Player> sorted, int count)
        {
            int taken = 0;
            foreach (Player player in sorted)
            {
                if (taken >= count)
                {
                    break;
                }

                if (used.Add(player.Id.Value))
                {
                    chosen.Add(player);
                    taken++;
                }
            }
        }

        private static List<Player> SortedByRating(Squad squad, Position? position)
        {
            var players = new List<Player>();
            foreach (Player player in squad.Players)
            {
                if (position == null || player.Position == position.Value)
                {
                    players.Add(player);
                }
            }

            players.Sort(CompareByRatingThenId);
            return players;
        }

        private static int CompareByRatingThenId(Player left, Player right)
        {
            int byRating = PlayerRatings.ForPosition(right).CompareTo(PlayerRatings.ForPosition(left));
            return byRating != 0 ? byRating : left.Id.Value.CompareTo(right.Id.Value);
        }
    }
}
