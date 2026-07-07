using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;

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
        private readonly LeagueTable _table;
        private readonly List<MatchResult> _playedResults;
        private int _currentRound;

        public LeagueSeason(League league)
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

            _table = new LeagueTable(clubIds);
            _playedResults = new List<MatchResult>();
        }

        public int CurrentRound => _currentRound;

        public int RoundCount => _fixturesByRound.Count;

        public bool IsComplete => _currentRound >= RoundCount;

        public LeagueTable Table => _table;

        public IReadOnlyList<MatchResult> PlayedResults => _playedResults;

        /// <summary>Rebuilds a season part-way through from its saved result history (save/load).</summary>
        public static LeagueSeason Restore(League league, int playedRounds, IReadOnlyList<MatchResult> playedResults)
        {
            var season = new LeagueSeason(league);
            foreach (MatchResult result in playedResults)
            {
                season._table.RecordMatch(result.Home, result.Away, result.HomeGoals, result.AwayGoals);
                season._playedResults.Add(result);
            }

            season._currentRound = playedRounds;
            return season;
        }

        public WeekResult AdvanceWeek(MatchSimulator simulator, MatchContext context, IRandom rng)
        {
            var matches = new List<MatchResult>();
            if (IsComplete)
            {
                return new WeekResult(_currentRound, matches);
            }

            foreach (Fixture fixture in _fixturesByRound[_currentRound])
            {
                Club home = _clubsById[fixture.Home];
                Club away = _clubsById[fixture.Away];
                var command = new MatchCommand(home.Strength, away.Strength, context);
                MatchOutcome outcome = simulator.Simulate(command, rng);

                _table.RecordMatch(fixture.Home, fixture.Away, outcome.HomeGoals, outcome.AwayGoals);
                matches.Add(new MatchResult(fixture.Home, fixture.Away, outcome.HomeGoals, outcome.AwayGoals));
            }

            _playedResults.AddRange(matches);
            int round = _currentRound;
            _currentRound++;
            return new WeekResult(round, matches);
        }
    }
}
