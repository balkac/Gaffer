using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Domain.Leagues;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// A save document turned back into run state: the rebuilt league (with full rosters), how far the
    /// season had been played, the result history to replay into the table, and the season number, so play
    /// continues into the right year across a multi-season save.
    /// <para>
    /// DATA, NOT A SEASON — deliberately. This type used to hand back a <see cref="LeagueSeason"/>, but the
    /// save adapter owns no simulator and must not invent one (ARCHITECTURE §6: a season's collaborators are
    /// settled at construction, by the run), so that season could not be played: calling
    /// <c>AdvanceWeek</c> on it threw. An object whose main verb throws is a trap no comment can make safe,
    /// so the shape carries only what a caller can actually use, and the one place that owns a simulator —
    /// <c>RunSession</c>, via <c>RunSessionFactory.Resume</c> — builds the playable season from these
    /// fields. A caller that wants a season back builds it with <see cref="LeagueSeason.Restore"/> and its
    /// own simulator, which is the same call the run makes.
    /// </para>
    /// </summary>
    public sealed class RestoredSeason
    {
        public RestoredSeason(League league, int seasonNumber, int playedRounds, IReadOnlyList<MatchResult> playedResults)
            : this(league, seasonNumber, playedRounds, playedResults, null)
        {
        }

        public RestoredSeason(League league, int seasonNumber, int playedRounds, IReadOnlyList<MatchResult> playedResults, RunState run)
        {
            League = league;
            SeasonNumber = seasonNumber;
            PlayedRounds = playedRounds;
            PlayedResults = playedResults;
            Run = run;
        }

        public League League { get; }

        public int SeasonNumber { get; }

        /// <summary>Rounds completed — the round a resumed season carries on from.</summary>
        public int PlayedRounds { get; }

        /// <summary>The results already played, in the order they were played, to replay into the table.</summary>
        public IReadOnlyList<MatchResult> PlayedResults { get; }

        /// <summary>
        /// The run around the season (v6): money, tactics, the eleven, morale, drama and the market. Null
        /// when the document carried no run block — a pre-v6 save read without going through
        /// <see cref="SaveMigrator"/>. It rides along here rather than in a second call because a save is
        /// one document and reading it twice is how two readers come to disagree about it.
        /// </summary>
        public RunState Run { get; }
    }
}
