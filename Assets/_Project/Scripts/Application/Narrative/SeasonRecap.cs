using System;
using System.Collections.Generic;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// What a season is remembered for: every moment it produced, across every career the run is
    /// following, in the order they happened (Faz 5.5).
    ///
    /// <para><b>Deliberately not a highlights reel.</b> It does not rank, score or trim — a season that
    /// produced four moments should read as a season that produced four moments, and a recap that always
    /// found five best bits would make a quiet year look like a good one. Choosing what to SHOW is the
    /// view's problem, and it can only make that choice honestly if it is handed everything.</para>
    ///
    /// <para>A projection over the journeys, built on demand and stored nowhere.</para>
    /// </summary>
    public sealed class SeasonRecap
    {
        private static readonly Comparison<CareerMoment> ByRound = (left, right) => left.Round.CompareTo(right.Round);

        private SeasonRecap(int season, IReadOnlyList<CareerMoment> moments)
        {
            Season = season;
            Moments = moments;
        }

        public int Season { get; }

        /// <summary>Everything that happened, earliest week first.</summary>
        public IReadOnlyList<CareerMoment> Moments { get; }

        public bool WasQuiet => Moments.Count == 0;

        /// <summary>
        /// Reads one season out of the log. Returns an empty recap rather than null for a season nothing
        /// happened in — "nothing happened" is an answer a view can render, and a null is one it has to
        /// branch on (CONVENTIONS §4).
        /// </summary>
        public static SeasonRecap Create(JourneyLog log, int season)
        {
            var moments = new List<CareerMoment>();
            if (log == null)
            {
                return new SeasonRecap(season, moments);
            }

            IReadOnlyList<PlayerJourney> journeys = log.Journeys;
            for (int j = 0; j < journeys.Count; j++)
            {
                IReadOnlyList<CareerMoment> theirs = journeys[j].Moments;
                for (int i = 0; i < theirs.Count; i++)
                {
                    if (theirs[i].Season == season)
                    {
                        moments.Add(theirs[i]);
                    }
                }
            }

            // A cached comparison delegate and List.Sort — the allocation-free overload (PERFORMANCE §8).
            // Stable enough for a recap: two moments in the same week are two things that happened that
            // week, and the log's own order is as good an answer as any for which to print first.
            moments.Sort(ByRound);
            return new SeasonRecap(season, moments);
        }
    }
}
