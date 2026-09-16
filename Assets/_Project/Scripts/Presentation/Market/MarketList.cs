using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Players;

namespace Gaffer.Presentation.Market
{
    /// <summary>
    /// The slice of the market a screen shows, and the order it shows it in.
    ///
    /// <para>Kept apart from the screen for the usual reason: which players a filter leaves and how a
    /// list is ordered are rules that fail quietly — a filter that dropped one line, or a sort that
    /// flickered between two equal ratings on every rebind, would still look like a list. Framework-free,
    /// so <c>dotnet test</c> holds both.</para>
    ///
    /// <para><b>Best first, by what the manager can see.</b> The rivals shop the market by visible
    /// ability (PROGRESS, 2026-08-16), and the manager reads it the same way: the number on the row is
    /// <see cref="PlayerRatings.ForRole(Player)"/>, and the list is that number descending. Ties break on
    /// id so two equal men keep their places between rebinds.</para>
    /// </summary>
    public static class MarketList
    {
        // Cached rather than passed as a method group: C# caches no method group, and List.Sort with a
        // fresh delegate on every rebuild is an allocation the market does not need (PERFORMANCE §8).
        private static readonly Comparison<Player> ByRatingDescending = CompareByRating;

        /// <summary>
        /// Everyone in <paramref name="source"/> on the given line (or every line when null) whom
        /// <paramref name="keep"/> also allows (or everyone when null), in source order, into
        /// <paramref name="into"/>, which is cleared first.
        /// </summary>
        public static void Select(IReadOnlyList<Player> source, Position? line, Predicate<Player> keep, List<Player> into)
        {
            into.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                Player player = source[i];
                if (line.HasValue && player.Position != line.Value)
                {
                    continue;
                }

                if (keep != null && !keep(player))
                {
                    continue;
                }

                into.Add(player);
            }
        }

        /// <summary>Best first by role rating; equal ratings keep a stable order by id.</summary>
        public static void SortByRating(List<Player> players)
        {
            players.Sort(ByRatingDescending);
        }

        /// <summary>
        /// The list in the order asked for. Youngest first and cheapest first both fall back to best first
        /// among equals, and every order ends on id, so the same list comes out of any starting order.
        /// The fee is asked of the caller because pricing belongs to the run's economy, not to a list.
        /// </summary>
        public static void Sort(List<Player> players, MarketSort order, Func<Player, long> fee)
        {
            switch (order)
            {
                case MarketSort.Age:
                    players.Sort(CompareByAge);
                    break;
                case MarketSort.Fee:
                    players.Sort((left, right) =>
                    {
                        int byFee = fee(left).CompareTo(fee(right));
                        return byFee != 0 ? byFee : CompareByRating(left, right);
                    });
                    break;
                default:
                    players.Sort(ByRatingDescending);
                    break;
            }
        }

        /// <summary>Whether a player's age falls in the band; <see cref="AgeBand.All"/> takes everyone.</summary>
        public static bool InAgeBand(Player player, AgeBand band)
        {
            switch (band)
            {
                case AgeBand.Under22:
                    return player.Age <= 21;
                case AgeBand.Prime:
                    return player.Age >= 22 && player.Age <= 28;
                case AgeBand.Veteran:
                    return player.Age >= 29;
                default:
                    return true;
            }
        }

        private static int CompareByRating(Player left, Player right)
        {
            int byRating = PlayerRatings.ForRole(right).CompareTo(PlayerRatings.ForRole(left));
            return byRating != 0 ? byRating : left.Id.Value.CompareTo(right.Id.Value);
        }

        private static int CompareByAge(Player left, Player right)
        {
            int byAge = left.Age.CompareTo(right.Age);
            return byAge != 0 ? byAge : CompareByRating(left, right);
        }
    }
}
