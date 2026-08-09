using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>Plays one season as a double round-robin and folds every result into the statistics.</summary>
    public sealed class SeasonRunner
    {
        private readonly MatchSimulator _simulator;
        private readonly MatchContext _context;

        public SeasonRunner(MatchSimulator simulator)
        {
            _simulator = simulator;
            _context = new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
        }

        public void RunSeason(IReadOnlyList<TeamProfile> teams, IRandom rng, HarnessStatistics statistics)
        {
            var standings = new Standings(teams);

            for (int home = 0; home < teams.Count; home++)
            {
                for (int away = 0; away < teams.Count; away++)
                {
                    if (home == away)
                    {
                        continue;
                    }

                    TeamProfile homeTeam = teams[home];
                    TeamProfile awayTeam = teams[away];
                    var command = new MatchCommand(homeTeam.Strength, awayTeam.Strength, _context);
                    MatchOutcome outcome = _simulator.Simulate(command, rng);

                    standings.ForRank(homeTeam.Rank).RecordResult(outcome.HomeGoals, outcome.AwayGoals);
                    standings.ForRank(awayTeam.Rank).RecordResult(outcome.AwayGoals, outcome.HomeGoals);
                    statistics.RecordMatch(homeTeam, awayTeam, outcome);
                }
            }

            IReadOnlyList<StandingsRow> finalTable = standings.Ordered();
            statistics.RecordSeason(finalTable);
        }
    }
}
