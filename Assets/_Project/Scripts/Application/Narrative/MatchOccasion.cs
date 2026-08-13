using Gaffer.Application.Simulation;
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
            MatchContext context)
        {
            Player = player;
            Club = club;
            Season = season;
            Round = round;
            AppearancesBefore = appearancesBefore;
            GoalsBefore = goalsBefore;
            GoalsInThisMatch = goalsInThisMatch;
            FirstGoalMinute = firstGoalMinute;
            Context = context;
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

        /// <summary>
        /// What the fixture WAS, not merely that it was something. Carrying the whole context rather than
        /// a single "big match" flag is what lets a derby goal, a title-decider goal and a relegation
        /// six-pointer goal be three different stories — collapsing them to one bool printed the same
        /// sentence four times in one career, which is a fixture list wearing prose (PROGRESS 2026-08-13).
        /// </summary>
        public MatchContext Context { get; }

        /// <summary>The fixture was a derby: the rivalry a club carries all run.</summary>
        public bool WasADerby => Context.IsRivalry || Context.Importance == MatchImportance.Derby;

        /// <summary>The fixture was decided something at the top.</summary>
        public bool WasATitleDecider => Context.IsTitleDecider || Context.Importance == MatchImportance.Final;

        /// <summary>The fixture was a relegation six-pointer.</summary>
        public bool WasARelegationSixPointer => Context.Importance == MatchImportance.RelegationSixPointer;

        /// <summary>
        /// The fixture was an occasion of some kind. Kept for a rule that wants "did this match matter at
        /// all" without caring which way — it is NOT a fourth case, and a rule guarding on
        /// <c>WasAnOccasion &amp;&amp; !derby &amp;&amp; !decider &amp;&amp; !sixPointer</c> is a
        /// contradiction, since this is exactly their union. One such rule existed and could never fire.
        /// </summary>
        public bool WasAnOccasion => WasADerby || WasATitleDecider || WasARelegationSixPointer;

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
