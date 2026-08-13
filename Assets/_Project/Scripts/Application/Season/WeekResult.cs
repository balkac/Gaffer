using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>The outcome of advancing one match week: the round played, its results, and what each
    /// fixture meant.</summary>
    public sealed class WeekResult
    {
        private static readonly IReadOnlyList<MatchContext> NoContexts = Array.Empty<MatchContext>();

        public WeekResult(int round, IReadOnlyList<MatchResult> matches, IReadOnlyList<MatchContext> contexts = null)
        {
            Round = round;
            Matches = matches;
            Contexts = contexts ?? NoContexts;
        }

        public int Round { get; }

        public IReadOnlyList<MatchResult> Matches { get; }

        /// <summary>
        /// What each fixture was, index-aligned with <see cref="Matches"/> — the occasion it was actually
        /// played on, after <see cref="MatchContextBuilder"/> raised the season's base context for a derby,
        /// a title decider or a relegation six-pointer.
        ///
        /// <para>It has to travel with the result. The narrative layer's whole question is "what was this
        /// match", and it was reading the season's BASE context — the one every fixture starts from — so
        /// no goal was ever recognised as having mattered even after the occasions went live
        /// (PROGRESS 2026-08-13). Transient, like the rest of this type: a save keeps the result, not the
        /// occasion, which is re-derivable from the table.</para>
        /// </summary>
        public IReadOnlyList<MatchContext> Contexts { get; }

        /// <summary>The occasion this club's fixture was played on, or <paramref name="fallback"/> when it
        /// did not play this week.</summary>
        public MatchContext ContextFor(ClubId club, MatchContext fallback)
        {
            for (int i = 0; i < Matches.Count && i < Contexts.Count; i++)
            {
                MatchResult match = Matches[i];
                if (match.Home == club || match.Away == club)
                {
                    return Contexts[i];
                }
            }

            return fallback;
        }
    }
}
