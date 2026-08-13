using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Works out what a fixture MEANT, one match at a time.
    ///
    /// <para><b>Why this exists.</b> A whole season used to be played on a single
    /// <see cref="MatchContext"/> — every fixture identical, none of them an occasion. Two things were
    /// dead because of it. The trait layer's context-sensitive players (the derby monster, the one who
    /// goes missing on the big day) had no big day to fire on, so traits meant to be mechanically real
    /// were flavour after all (NON-NEGOTIABLE #7). And the narrative layer's "what the match was" input
    /// was inert, so no career ever recorded a goal that mattered — measured with the Gate B probe over
    /// twenty seasons: not one, and the arcs read like stat sheets (PROGRESS 2026-08-13).</para>
    ///
    /// <para><b>Three ways a league match can matter</b>, and all three are read from what the season
    /// already knows — no new data, no authored fixtures. A DERBY is the rivalry a club carries all run
    /// (<see cref="RivalryTable"/>), and is the only one that does not depend on form. A TITLE DECIDER and
    /// a RELEGATION SIX-POINTER are both "two clubs with the same thing at stake, late enough for it to be
    /// settled" — the run-in, from <see cref="MatchContextSettings"/>, because two clubs top of the table
    /// in September are not deciding anything.</para>
    ///
    /// <para>The flags compose and the importance does not: a derby between two contenders in May is a
    /// rivalry AND a decider, but a match has one importance, so the strongest wins. Pure and
    /// deterministic — same table, same fixture, same answer.</para>
    /// </summary>
    public sealed class MatchContextBuilder
    {
        private readonly RivalryTable _rivalries;
        private readonly MatchContextSettings _settings;

        public MatchContextBuilder(RivalryTable rivalries)
            : this(rivalries, MatchContextSettings.Default)
        {
        }

        public MatchContextBuilder(RivalryTable rivalries, MatchContextSettings settings)
        {
            _rivalries = rivalries ?? RivalryTable.None;
            _settings = settings ?? MatchContextSettings.Default;
        }

        /// <summary>
        /// The context this fixture is played on: <paramref name="baseContext"/> — the season's own
        /// occasion and crowd — raised by whatever this particular match is.
        /// </summary>
        /// <param name="standings">The table as it stands, best first. Passed in rather than looked up so
        /// a week's worth of fixtures orders it once between them.</param>
        public MatchContext Build(MatchContext baseContext, ClubId home, ClubId away, IReadOnlyList<LeagueTableRow> standings, int round, int roundCount)
        {
            bool derby = _rivalries.AreRivals(home, away);
            bool decider = false;
            bool sixPointer = false;

            if (IsInTheRunIn(round, roundCount) && standings != null)
            {
                int homePlace = PlaceOf(home, standings);
                int awayPlace = PlaceOf(away, standings);
                if (homePlace > 0 && awayPlace > 0)
                {
                    decider = IsAContender(homePlace) && IsAContender(awayPlace);
                    sixPointer = IsInTheDrop(homePlace, standings.Count) && IsInTheDrop(awayPlace, standings.Count);
                }
            }

            if (!derby && !decider && !sixPointer)
            {
                return baseContext;
            }

            return new MatchContext(
                StrongestOf(baseContext.Importance, derby, decider, sixPointer),
                baseContext.CrowdSize,
                isTitleDecider: baseContext.IsTitleDecider || decider,
                isRivalry: baseContext.IsRivalry || derby);
        }

        // A match has one importance, so when a fixture is several things at once the biggest occasion
        // wins the label. A title decider outranks a relegation six-pointer outranks a derby; a context
        // that already arrived raised (a cup final, one day) is never lowered.
        private static MatchImportance StrongestOf(MatchImportance current, bool derby, bool decider, bool sixPointer)
        {
            if (current == MatchImportance.Final)
            {
                return current;
            }

            if (decider)
            {
                return MatchImportance.Final;
            }

            if (sixPointer)
            {
                return MatchImportance.RelegationSixPointer;
            }

            return derby ? MatchImportance.Derby : current;
        }

        private bool IsInTheRunIn(int round, int roundCount)
        {
            return roundCount > 0 && round >= roundCount - _settings.RunInRounds;
        }

        private bool IsAContender(int place)
        {
            return place <= _settings.TitleContenders;
        }

        private bool IsInTheDrop(int place, int clubCount)
        {
            return place > clubCount - _settings.RelegationPlaces;
        }

        // 1-based, or 0 when the club is not in this table.
        private static int PlaceOf(ClubId club, IReadOnlyList<LeagueTableRow> standings)
        {
            for (int i = 0; i < standings.Count; i++)
            {
                if (standings[i].Club == club)
                {
                    return i + 1;
                }
            }

            return 0;
        }
    }
}
