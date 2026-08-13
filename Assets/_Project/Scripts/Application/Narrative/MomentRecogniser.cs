using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// Turns a played match into the moments it produced, and writes them into the journeys they belong
    /// to. The memory half of "Story = Simulation + Character + Memory" (CLAUDE.md).
    ///
    /// <para><b>It changes nothing about the game.</b> The match is over, the goals are scored, the table
    /// is written. Nothing here can move a scoreline, so the narrative can be retuned as often as the
    /// copy needs without touching a calibrated number — the design axiom, built ON the simulation rather
    /// than at its expense.</para>
    ///
    /// <para><b>What it owns, and what it does not.</b> It owns the ORDER: the occasion is snapshotted
    /// before the counters move, every rule is asked about that snapshot, and only then are the
    /// appearance and the goals added. That ordering is the whole difference between a debut and a
    /// fiftieth appearance, and it has one owner here rather than a copy inside each rule
    /// (ARCHITECTURE §8a). It does not own WHICH moments exist — that is <see cref="IMomentRule"/>, so a
    /// new kind of moment is a new class rather than an edit to this one.</para>
    ///
    /// <para>Deterministic: no randomness at all. Allocation-light on the weekly path — the per-match
    /// scratch and the returned list are reused (PERFORMANCE §8), and the rules are built once.</para>
    /// </summary>
    public sealed class MomentRecogniser
    {
        // The shipped vocabulary, TUNED BY READING IT (Gate B, 2026-08-13). Two changes, both cuts:
        //
        // A BRACE IS NOT A MOMENT. Measured across 67 careers, it was 20.2% of everything recognised — a
        // good striker scores twice several times a season, so it is a Tuesday, and the strongest career
        // read "scored twice" fourteen times. Nothing in a list feels rare, which is the exact bet Gate B
        // settles. A brace that happened in a fixture worth naming is still told, by the occasion rules.
        // Compare Hattrick at 0.7%: THAT is rare, and reads it.
        //
        // APPEARANCE MILESTONES START AT 100. They were 24.6% on 25/50/100/200 — a 25th game is not an
        // occasion, it is arithmetic, and at a match a week the early ones arrive in a clump.
        private static readonly IMomentRule[] DefaultRules =
        {
            new DebutRule(),
            new FirstGoalRule(),
            new HattrickRule(),
            new DerbyGoalRule(),
            new TitleDeciderGoalRule(),
            new RelegationGoalRule(),
            new BigMatchGoalRule(),
            MilestoneRule.ForAppearances(100, 200, 300),
            MilestoneRule.ForGoals(10, 25, 50, 100),
        };

        private readonly IMomentRule[] _rules;

        // Goals per player in the match being read, cleared and refilled per call rather than allocated.
        // Squad-sized, not world-sized: one club's match is read at a time.
        private readonly Dictionary<int, int> _goalsThisMatch = new Dictionary<int, int>();
        private readonly List<CareerMoment> _recognised = new List<CareerMoment>();

        public MomentRecogniser()
            : this(DefaultRules)
        {
        }

        /// <summary>Runs a specific set of rules — how a test pins one kind of moment with the others out
        /// of the way, and the seam a wider, data-driven vocabulary arrives through later.</summary>
        public MomentRecogniser(IMomentRule[] rules)
        {
            _rules = rules ?? DefaultRules;
        }

        /// <summary>
        /// Reads one club's match and returns the moments it produced, having already recorded them in
        /// the log.
        ///
        /// <para>The returned list is REUSED and valid only until the next call — the same contract
        /// <c>LineupSelector.SelectBest</c> carries. A caller that keeps it copies it out.</para>
        /// </summary>
        /// <param name="starters">The eleven that played, so a substitute who never came on cannot
        /// collect a debut he did not have.</param>
        public IReadOnlyList<CareerMoment> Recognise(
            JourneyLog log,
            ClubId club,
            IReadOnlyList<Player> starters,
            MatchResult match,
            MatchContext context,
            int season,
            int round)
        {
            _recognised.Clear();
            _goalsThisMatch.Clear();
            if (log == null || starters == null)
            {
                return _recognised;
            }

            TallyGoalsBy(club, match);

            for (int i = 0; i < starters.Count; i++)
            {
                Player player = starters[i];
                PlayerJourney journey = log.Follow(player.Id, player.Name);
                _goalsThisMatch.TryGetValue(player.Id.Value, out int goals);

                var asked = new MatchOccasion(
                    player,
                    club,
                    season,
                    round,
                    appearancesBefore: journey.Appearances,
                    goalsBefore: journey.Goals,
                    goalsInThisMatch: goals,
                    firstGoalMinute: goals > 0 ? FirstGoalMinuteOf(player.Id, club, match) : CareerMoment.NoMinute,
                    context: context);

                for (int rule = 0; rule < _rules.Length; rule++)
                {
                    if (_rules[rule].Recognise(in asked, out CareerMoment moment))
                    {
                        journey.Add(moment);
                        _recognised.Add(moment);
                    }
                }

                // The counters move only once every rule has seen the "before" picture.
                journey.RecordAppearance(season);
                if (goals > 0)
                {
                    journey.RecordGoals(season, goals);
                }
            }

            return _recognised;
        }


        // Goals by this club's players, read from the events rather than the scoreline because only the
        // events name anyone. A match played without squads carries no scorer at all (MatchEvent.Scorer
        // is null there) and so produces no moments — which is correct: nothing happened to anybody.
        private void TallyGoalsBy(ClubId club, MatchResult match)
        {
            IReadOnlyList<MatchEvent> events = match.Events;
            if (events == null)
            {
                return;
            }

            TeamSide side = match.Home == club ? TeamSide.Home : TeamSide.Away;
            for (int i = 0; i < events.Count; i++)
            {
                MatchEvent played = events[i];
                if (played.Kind != MatchEventKind.Goal || played.Side != side || !played.Scorer.HasValue)
                {
                    continue;
                }

                int id = played.Scorer.Value.Value;
                _goalsThisMatch.TryGetValue(id, out int already);
                _goalsThisMatch[id] = already + 1;
            }
        }

        private static int FirstGoalMinuteOf(PlayerId scorer, ClubId club, MatchResult match)
        {
            IReadOnlyList<MatchEvent> events = match.Events;
            if (events == null)
            {
                return CareerMoment.NoMinute;
            }

            TeamSide side = match.Home == club ? TeamSide.Home : TeamSide.Away;
            for (int i = 0; i < events.Count; i++)
            {
                MatchEvent played = events[i];
                if (played.Kind == MatchEventKind.Goal && played.Side == side && played.Scorer.HasValue && played.Scorer.Value == scorer)
                {
                    return played.Minute;
                }
            }

            return CareerMoment.NoMinute;
        }
    }
}
