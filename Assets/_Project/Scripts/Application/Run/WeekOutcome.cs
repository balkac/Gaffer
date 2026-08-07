using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Season;
using Gaffer.Application.Transfers;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// Everything one played match week changed, as an immutable record the view replays (ARCHITECTURE
    /// §8, CLAUDE.md NON-NEGOTIABLE #4). The fields below are the ones the editor windows used to derive
    /// for themselves by scanning the season's live <c>Table</c> and <c>PlayedResults</c> — league
    /// position and the losing streak — plus the ones they simply forgot to keep in step (the wage
    /// payment, the verdict, whether the window has opened). Deriving them here means the drama engine
    /// and the view can no longer disagree about what week it is.
    /// </summary>
    public sealed class WeekOutcome
    {
        /// <summary>
        /// Every value is required: this record is only ever built by <see cref="RunSession"/> at the end
        /// of a played week, where all of it is known, so there is no default worth having and an omitted
        /// field should not compile.
        /// </summary>
        public WeekOutcome(
            int round,
            int playedRounds,
            int roundCount,
            bool isSeasonComplete,
            IReadOnlyList<MatchResult> matches,
            MatchResult? managedMatch,
            int tablePosition,
            int lossStreak,
            Finances finances,
            long wagesPaid,
            TransferWindowPhase windowPhase,
            PendingDrama drama,
            SeasonVerdict? verdict,
            int finalPosition)
        {
            Round = round;
            PlayedRounds = playedRounds;
            RoundCount = roundCount;
            IsSeasonComplete = isSeasonComplete;
            Matches = matches;
            ManagedMatch = managedMatch;
            TablePosition = tablePosition;
            LossStreak = lossStreak;
            Finances = finances;
            WagesPaid = wagesPaid;
            WindowPhase = windowPhase;
            Drama = drama;
            Verdict = verdict;
            FinalPosition = finalPosition;
        }

        /// <summary>The round that was played, 0-based (round + 1 is the "week N" a view shows).</summary>
        public int Round { get; }

        /// <summary>Rounds played after this week.</summary>
        public int PlayedRounds { get; }

        /// <summary>Rounds in the season.</summary>
        public int RoundCount { get; }

        public bool IsSeasonComplete { get; }

        /// <summary>Every fixture played this round.</summary>
        public IReadOnlyList<MatchResult> Matches { get; }

        /// <summary>The managed club's fixture this round, or null if it did not play.</summary>
        public MatchResult? ManagedMatch { get; }

        /// <summary>The managed club's league position after this week, 1-based; 0 when unknown.</summary>
        public int TablePosition { get; }

        /// <summary>Consecutive league defeats the managed club is now on.</summary>
        public int LossStreak { get; }

        /// <summary>The club's money after the week's wages were paid.</summary>
        public Finances Finances { get; }

        /// <summary>What the wage bill took out of the transfer cash this week (GDD §4.4).</summary>
        public long WagesPaid { get; }

        /// <summary>Which transfer window (if any) is open now that the round has been played.</summary>
        public TransferWindowPhase WindowPhase { get; }

        /// <summary>
        /// The drama raised this week, or null for a quiet week — by design the common case. A raised
        /// event blocks the next week until <see cref="RunSession.ResolveDrama"/> answers it.
        /// </summary>
        public PendingDrama Drama { get; }

        /// <summary>The board's verdict, set on the week that completes the season; null otherwise.</summary>
        public SeasonVerdict? Verdict { get; }

        /// <summary>The managed club's final position, set with <see cref="Verdict"/>; 0 otherwise.</summary>
        public int FinalPosition { get; }
    }
}
