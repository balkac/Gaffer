using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// Everything a rule is allowed to ask about one player's match — a snapshot taken BEFORE the
    /// journey's counters move, which is what lets "was this his first goal" be a question at all.
    ///
    /// <para>A rule never touches the journey and never counts anything. The recogniser owns the order in
    /// which appearances and goals are added, because that order is the difference between a debut and a
    /// fiftieth appearance, and an ordering with one owner is the rule this codebase keeps relearning
    /// (ARCHITECTURE §8a). Rules are pure predicates over this; that is what makes each one testable on
    /// its own and impossible to break by adding another.</para>
    /// </summary>
    public readonly struct MatchOccasion
    {
        public MatchOccasion(
            Player player,
            ClubId club,
            int season,
            int round,
            int appearancesBefore,
            int goalsBefore,
            int goalsInThisMatch,
            int firstGoalMinute,
            bool isABigMatch)
        {
            Player = player;
            Club = club;
            Season = season;
            Round = round;
            AppearancesBefore = appearancesBefore;
            GoalsBefore = goalsBefore;
            GoalsInThisMatch = goalsInThisMatch;
            FirstGoalMinute = firstGoalMinute;
            IsABigMatch = isABigMatch;
        }

        public Player Player { get; }

        public ClubId Club { get; }

        public int Season { get; }

        public int Round { get; }

        /// <summary>Appearances he had made before this match. Zero means this one is his debut.</summary>
        public int AppearancesBefore { get; }

        /// <summary>Goals he had scored before this match. Zero plus a goal today means his first.</summary>
        public int GoalsBefore { get; }

        public int GoalsInThisMatch { get; }

        public bool Scored => GoalsInThisMatch > 0;

        /// <summary>The minute of his first goal today, or <see cref="CareerMoment.NoMinute"/>.</summary>
        public int FirstGoalMinute { get; }

        /// <summary>Whether the fixture itself was an occasion — a rivalry, a decider, a relegation six-pointer.</summary>
        public bool IsABigMatch { get; }

        public int AppearancesAfter => AppearancesBefore + 1;

        public int GoalsAfter => GoalsBefore + GoalsInThisMatch;

        /// <summary>Builds the moment this occasion describes, so a rule states WHAT it recognised and
        /// never has to restate WHO or WHEN.</summary>
        public CareerMoment Moment(CareerMomentKind kind, int minute = CareerMoment.NoMinute, long count = 0L)
        {
            return new CareerMoment(kind, Player.Id, Club, Season, Round, minute, count);
        }
    }
}
