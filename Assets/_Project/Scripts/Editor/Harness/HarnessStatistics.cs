using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;

namespace Gaffer.Editor.Harness
{
    /// <summary>
    /// Folds every match and season into running totals, then turns them into the immutable
    /// <see cref="HarnessReport"/> — the distributions Gate A judges (goals, favourite bias, home
    /// advantage, title spread).
    /// </summary>
    public sealed class HarnessStatistics
    {
        public const int GoalBucketCap = 10; // bucket GoalBucketCap = "GoalBucketCap+"

        private readonly long[] _goalsHistogram = new long[GoalBucketCap + 1];
        private readonly long[] _titlesByRank;

        private long _totalMatches;
        private long _totalGoals;
        private long _homeWins;
        private long _draws;
        private long _awayWins;
        private long _favouriteWins;
        private long _favouriteDraws;
        private long _favouriteUpsets;
        private IReadOnlyList<StandingsRow> _sampleTable;

        public HarnessStatistics(int teamCount)
        {
            _titlesByRank = new long[teamCount];
        }

        public void RecordMatch(TeamProfile home, TeamProfile away, MatchOutcome outcome)
        {
            _totalMatches++;
            int goals = outcome.HomeGoals + outcome.AwayGoals;
            _totalGoals += goals;
            _goalsHistogram[Math.Min(goals, GoalBucketCap)]++;

            if (outcome.HomeGoals > outcome.AwayGoals)
            {
                _homeWins++;
            }
            else if (outcome.HomeGoals < outcome.AwayGoals)
            {
                _awayWins++;
            }
            else
            {
                _draws++;
            }

            if (home.BaseQuality != away.BaseQuality)
            {
                bool homeIsFavourite = home.BaseQuality > away.BaseQuality;
                int favouriteGoals = homeIsFavourite ? outcome.HomeGoals : outcome.AwayGoals;
                int underdogGoals = homeIsFavourite ? outcome.AwayGoals : outcome.HomeGoals;

                if (favouriteGoals > underdogGoals)
                {
                    _favouriteWins++;
                }
                else if (favouriteGoals == underdogGoals)
                {
                    _favouriteDraws++;
                }
                else
                {
                    _favouriteUpsets++;
                }
            }
        }

        public void RecordSeason(IReadOnlyList<StandingsRow> finalTable)
        {
            _titlesByRank[finalTable[0].Team.Rank]++;
            if (_sampleTable == null)
            {
                _sampleTable = finalTable;
            }
        }

        public HarnessReport BuildReport(HarnessConfig config, IReadOnlyList<TeamProfile> teams)
        {
            long seasons = 0;
            foreach (long titles in _titlesByRank)
            {
                seasons += titles;
            }

            var goalBins = new List<HistogramBin>(_goalsHistogram.Length);
            for (int goals = 0; goals < _goalsHistogram.Length; goals++)
            {
                string label = goals == GoalBucketCap ? goals + "+" : goals.ToString();
                goalBins.Add(new HistogramBin(label, _goalsHistogram[goals], Percentage(_goalsHistogram[goals], _totalMatches)));
            }

            var championShares = new List<ChampionShare>(teams.Count);
            int distinctWinners = 0;
            for (int rank = 0; rank < teams.Count; rank++)
            {
                long titles = _titlesByRank[rank];
                if (titles > 0)
                {
                    distinctWinners++;
                }

                championShares.Add(new ChampionShare(rank, teams[rank].Name, titles, Percentage(titles, seasons)));
            }

            var sampleTable = new List<TableRowView>(_sampleTable.Count);
            for (int i = 0; i < _sampleTable.Count; i++)
            {
                sampleTable.Add(new TableRowView(i + 1, _sampleTable[i].Team, _sampleTable[i]));
            }

            double averageGoals = _totalMatches == 0 ? 0.0 : (double)_totalGoals / _totalMatches;
            double favouriteWinPct = Percentage(_favouriteWins, _favouriteWins + _favouriteDraws + _favouriteUpsets);
            double homeWinPct = Percentage(_homeWins, _totalMatches);
            double awayWinPct = Percentage(_awayWins, _totalMatches);
            double topSeedShare = Percentage(_titlesByRank[0], seasons);

            var gateChecks = new List<GateCheck>
            {
                BuildGoalsCheck(averageGoals),
                BuildFavouriteCheck(favouriteWinPct),
                BuildHomeAdvantageCheck(homeWinPct, awayWinPct),
                BuildTitleRaceCheck(topSeedShare, distinctWinners),
            };

            return new HarnessReport(
                config,
                _totalMatches,
                averageGoals,
                goalBins,
                homeWinPct,
                Percentage(_draws, _totalMatches),
                awayWinPct,
                favouriteWinPct,
                Percentage(_favouriteDraws, _favouriteWins + _favouriteDraws + _favouriteUpsets),
                Percentage(_favouriteUpsets, _favouriteWins + _favouriteDraws + _favouriteUpsets),
                championShares,
                sampleTable,
                gateChecks);
        }

        private static GateCheck BuildGoalsCheck(double averageGoals)
        {
            GateStatus status = InBand(averageGoals, 2.3, 3.3) ? GateStatus.Pass
                : InBand(averageGoals, 1.8, 3.8) ? GateStatus.Warn
                : GateStatus.Fail;
            return new GateCheck("Goals per match", averageGoals.ToString("F2"), "target ~2.5–3.0", status);
        }

        private static GateCheck BuildFavouriteCheck(double favouriteWinPct)
        {
            GateStatus status = InBand(favouriteWinPct, 45.0, 63.0) ? GateStatus.Pass
                : InBand(favouriteWinPct, 40.0, 70.0) ? GateStatus.Warn
                : GateStatus.Fail;
            return new GateCheck("Favourite win rate", favouriteWinPct.ToString("F1") + "%", "stronger side usually wins, upsets stay credible", status);
        }

        private static GateCheck BuildHomeAdvantageCheck(double homeWinPct, double awayWinPct)
        {
            GateStatus status = homeWinPct > awayWinPct + 3.0 ? GateStatus.Pass
                : homeWinPct > awayWinPct ? GateStatus.Warn
                : GateStatus.Fail;
            return new GateCheck("Home advantage", homeWinPct.ToString("F1") + "% vs " + awayWinPct.ToString("F1") + "%", "home wins outweigh away", status);
        }

        private static GateCheck BuildTitleRaceCheck(double topSeedShare, int distinctWinners)
        {
            GateStatus status = topSeedShare <= 75.0 && distinctWinners >= 3 ? GateStatus.Pass
                : topSeedShare <= 88.0 ? GateStatus.Warn
                : GateStatus.Fail;
            return new GateCheck("Title race", "top seed " + topSeedShare.ToString("F0") + "%", distinctWinners + " clubs have won it", status);
        }

        private static bool InBand(double value, double low, double high)
        {
            return value >= low && value <= high;
        }

        private static double Percentage(long count, long total)
        {
            return total == 0 ? 0.0 : 100.0 * count / total;
        }
    }
}
