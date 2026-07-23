using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Drives one league season week by week: schedules the double round-robin, and each
    /// <see cref="AdvanceWeek"/> simulates that round's fixtures on the injected core, folds the
    /// results into the table, and returns them for the presentation to replay. Keeps a result
    /// history so a season can be captured and restored (save/load). Deterministic — the same league
    /// and rng stream reproduce the same season.
    /// </summary>
    public sealed class LeagueSeason
    {
        private readonly Dictionary<ClubId, Club> _clubsById;
        private readonly Dictionary<int, List<Fixture>> _fixturesByRound;
        private readonly Dictionary<ClubId, Tactics> _tacticsByClub;
        private readonly Dictionary<ClubId, Formation> _formationByClub;
        private readonly Dictionary<ClubId, IReadOnlyList<Player>> _startersByClub;
        private readonly EffectiveStrengthBuilder _strengthBuilder;
        private readonly LineupSelector _lineupSelector;
        private readonly LeagueTable _table;
        private readonly List<MatchResult> _playedResults;

        // One generator reseeded per fixture instead of a fresh instance per match — the doc's
        // reseed-don't-re-new rule (PERFORMANCE §8). Reseeding is identical to constructing anew,
        // so the per-fixture streams (and determinism) are unchanged.
        private readonly SplitMix64RandomNumberGenerator _matchRng = new SplitMix64RandomNumberGenerator(0);
        private int _currentRound;

        public LeagueSeason(League league)
            : this(league, null)
        {
        }

        /// <summary>Runs the season on a specific trait catalog (from config assets) — the strength step
        /// resolves each player's traits through it. Null falls back to the built-in default.</summary>
        public LeagueSeason(League league, Gaffer.Domain.Traits.TraitCatalog traits)
        {
            _clubsById = new Dictionary<ClubId, Club>(league.Clubs.Count);
            var clubIds = new List<ClubId>(league.Clubs.Count);
            foreach (Club club in league.Clubs)
            {
                _clubsById[club.Id] = club;
                clubIds.Add(club.Id);
            }

            IReadOnlyList<Fixture> fixtures = new FixtureScheduler().CreateDoubleRoundRobin(clubIds);
            _fixturesByRound = new Dictionary<int, List<Fixture>>();
            foreach (Fixture fixture in fixtures)
            {
                if (!_fixturesByRound.TryGetValue(fixture.Round, out List<Fixture> roundFixtures))
                {
                    roundFixtures = new List<Fixture>();
                    _fixturesByRound[fixture.Round] = roundFixtures;
                }

                roundFixtures.Add(fixture);
            }

            _tacticsByClub = new Dictionary<ClubId, Tactics>();
            _formationByClub = new Dictionary<ClubId, Formation>();
            _startersByClub = new Dictionary<ClubId, IReadOnlyList<Player>>();
            _strengthBuilder = traits == null ? new EffectiveStrengthBuilder() : new EffectiveStrengthBuilder(traits);
            _lineupSelector = new LineupSelector();
            _table = new LeagueTable(clubIds);
            _playedResults = new List<MatchResult>();
            Morale = new MoraleLedger();
        }

        /// <summary>
        /// The season's live morale state — drama effects land here and the weekly strength
        /// derivation reads it, so a resolved event is felt in the coming weeks' results and fades
        /// on schedule. Transient (not yet persisted in a save — like tactics and the economy).
        /// </summary>
        public MoraleLedger Morale { get; }

        /// <summary>Sets a club's tactics; its match strength is re-derived from its lineup each round.</summary>
        public void SetTactics(ClubId club, Tactics tactics)
        {
            _tacticsByClub[club] = tactics;
        }

        /// <summary>Sets a club's formation, used to auto-pick its eleven when no explicit lineup is set.</summary>
        public void SetFormation(ClubId club, Formation formation)
        {
            _formationByClub[club] = formation;
        }

        /// <summary>Sets the exact eleven a club fields; overrides the auto-pick until changed.</summary>
        public void SetStarters(ClubId club, IReadOnlyList<Player> starters)
        {
            _startersByClub[club] = starters;
        }

        /// <summary>
        /// Replaces a club's roster mid-season (a signing or a sale). The club is rebuilt immutably around the
        /// new squad and any pinned lineup is dropped, so the next <see cref="AdvanceWeek"/> re-picks the eleven
        /// and re-derives the club's strength from the new roster — the change takes effect live, the same week
        /// it is made. No-op for a squad-less (strength-only) club.
        /// </summary>
        public void UpdateSquad(ClubId club, Squad squad)
        {
            if (!_clubsById.TryGetValue(club, out Club current) || current.Squad == null)
            {
                return;
            }

            _clubsById[club] = new Club(current.Id, current.Name, squad, current.Strength);
            _startersByClub.Remove(club);
        }

        /// <summary>The club's current roster, or <c>null</c> for a squad-less (strength-only) club.</summary>
        public Squad SquadOf(ClubId club)
        {
            return _clubsById.TryGetValue(club, out Club found) ? found.Squad : null;
        }

        public int CurrentRound => _currentRound;

        public int RoundCount => _fixturesByRound.Count;

        public bool IsComplete => _currentRound >= RoundCount;

        public LeagueTable Table => _table;

        public IReadOnlyList<MatchResult> PlayedResults => _playedResults;

        /// <summary>Rebuilds a season part-way through from its saved result history (save/load).</summary>
        public static LeagueSeason Restore(League league, int playedRounds, IReadOnlyList<MatchResult> playedResults, Gaffer.Domain.Traits.TraitCatalog traits = null)
        {
            var season = new LeagueSeason(league, traits);
            foreach (MatchResult result in playedResults)
            {
                season._table.RecordMatch(result.Home, result.Away, result.HomeGoals, result.AwayGoals);
                season._playedResults.Add(result);
            }

            season._currentRound = playedRounds;
            return season;
        }

        public WeekResult AdvanceWeek(MatchSimulator simulator, MatchContext context, ulong seasonSeed)
        {
            if (IsComplete)
            {
                return new WeekResult(_currentRound, new List<MatchResult>());
            }

            List<Fixture> roundFixtures = _fixturesByRound[_currentRound];
            var matches = new List<MatchResult>(roundFixtures.Count);
            for (int i = 0; i < roundFixtures.Count; i++)
            {
                Fixture fixture = roundFixtures[i];
                Club home = _clubsById[fixture.Home];
                Club away = _clubsById[fixture.Away];
                var command = new MatchCommand(
                    StrengthOf(home, context), StrengthOf(away, context),
                    home.Squad, away.Squad,
                    ProfileOf(home.Id), ProfileOf(away.Id),
                    context);

                // Each match gets its own rng stream, seeded from a stable function of the fixture's identity,
                // so a change to one club's tactics only reshapes its own matches — the rest of the league's
                // results stay byte-identical, and a resumed save reproduces the remaining fixtures exactly.
                _matchRng.Reseed(MixSeed(seasonSeed, _currentRound, fixture.Home.Value, fixture.Away.Value));
                MatchOutcome outcome = simulator.Simulate(command, _matchRng);

                _table.RecordMatch(fixture.Home, fixture.Away, outcome.HomeGoals, outcome.AwayGoals);
                matches.Add(new MatchResult(fixture.Home, fixture.Away, outcome.HomeGoals, outcome.AwayGoals, outcome.HomeShots, outcome.AwayShots, outcome.Events));
            }

            _playedResults.AddRange(matches);
            // The played week ages morale one step — a wound (or a high) from drama fades on schedule.
            Morale.TickWeek();
            int round = _currentRound;
            _currentRound++;
            return new WeekResult(round, matches);
        }

        // Avalanche-mixes the season seed with the fixture's identity into a well-distributed per-match seed
        // (SplitMix64 finalizer stages). Independent of any other match's draws, so match order and tactical
        // changes elsewhere never shift this match's stream.
        private static ulong MixSeed(ulong seasonSeed, int round, int home, int away)
        {
            unchecked
            {
                ulong z = seasonSeed + 0x9E3779B97F4A7C15UL;
                z ^= (ulong)(uint)round * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z ^= (ulong)(uint)home * 0x94D049BB133111EBUL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                z ^= (ulong)(uint)away;
                return z ^ (z >> 31);
            }
        }

        // A club with a squad fields an eleven — the manager's explicit lineup, or the best auto-pick for its
        // formation — and its strength is derived fresh from that eleven + tactics each round, so a change
        // takes effect the next week. The match context is threaded through so context-sensitive traits fire
        // on the occasion, not in every fixture. A squad-less club (restored, or a strength-only fixture)
        // keeps its precomputed strength.
        private TeamStrength StrengthOf(Club club, MatchContext context)
        {
            if (club.Squad == null)
            {
                return club.Strength;
            }

            return _strengthBuilder.Build(StartersOf(club), TacticsOf(club.Id), context, Morale);
        }

        private IReadOnlyList<Player> StartersOf(Club club)
        {
            if (_startersByClub.TryGetValue(club.Id, out IReadOnlyList<Player> starters))
            {
                return starters;
            }

            return _lineupSelector.SelectBest(club.Squad, FormationOf(club.Id));
        }

        private Formation FormationOf(ClubId club)
        {
            return _formationByClub.TryGetValue(club, out Formation formation) ? formation : Formation.F442;
        }

        private Tactics TacticsOf(ClubId club)
        {
            return _tacticsByClub.TryGetValue(club, out Tactics tactics) ? tactics : Tactics.Balanced;
        }

        private ChanceProfile ProfileOf(ClubId club)
        {
            return ChanceProfile.FromTactics(TacticsOf(club));
        }
    }
}
