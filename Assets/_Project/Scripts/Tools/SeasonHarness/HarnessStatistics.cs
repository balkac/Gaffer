using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;

namespace Gaffer.Tools.SeasonHarness
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

            // Titles are counted by TeamProfile.Rank (RecordSeason), so the club a row NAMES has to be
            // found by rank too. Reading teams[rank] instead was a latent mislabelling: it agrees only
            // while a factory happens to emit the list in rank order, and a factory that sorts by anything
            // else — strength, name, id — would have printed one club's name against another's titles.
            IReadOnlyList<TeamProfile> byRank = OrderByRank(teams);

            var championShares = new List<ChampionShare>(byRank.Count);
            int distinctWinners = 0;
            for (int rank = 0; rank < byRank.Count; rank++)
            {
                long titles = _titlesByRank[rank];
                if (titles > 0)
                {
                    distinctWinners++;
                }

                championShares.Add(new ChampionShare(
                    rank,
                    byRank[rank].Name,
                    titles,
                    Percentage(titles, seasons),
                    MeanStrength(byRank[rank])));
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
                BuildRankOrderCheck(byRank),
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

        /// <summary>
        /// Guards the MEANING of "titles by pre-season rank": every rank must actually be stronger than the
        /// rank below it. Without this the title table can be read as a sim bias when it is really a
        /// mislabelled league — a second seed handed a stronger squad than the first will out-title it
        /// every season of the run, and no sample size will wash that out, because the strengths are drawn
        /// once and reused for every season.
        /// </summary>
        private static GateCheck BuildRankOrderCheck(IReadOnlyList<TeamProfile> teams)
        {
            int inversions = 0;
            for (int rank = 1; rank < teams.Count; rank++)
            {
                if (MeanStrength(teams[rank]) > MeanStrength(teams[rank - 1]))
                {
                    inversions++;
                }
            }

            GateStatus status = inversions == 0 ? GateStatus.Pass : GateStatus.Fail;
            return new GateCheck(
                "Rank order",
                inversions + " inversions",
                "each rank is stronger than the one below it",
                status);
        }

        // The one number the sim is handed, collapsed to a scalar: the three axes weigh equally in the
        // chance model (attack over opponent defence, midfield share), so their mean is the fair summary.
        private static double MeanStrength(TeamProfile team)
        {
            return (team.Strength.Attack + team.Strength.Midfield + team.Strength.Defence) / 3.0;
        }

        // The teams by their own Rank rather than by list position. Ranks outside the league, or two teams
        // claiming one rank, would leave holes; those fall back to the team at that position rather than
        // throwing (CONVENTIONS §6), so a malformed league still renders a report instead of taking a
        // 1000-season run down with an exception on the last line.
        private static IReadOnlyList<TeamProfile> OrderByRank(IReadOnlyList<TeamProfile> teams)
        {
            var byRank = new TeamProfile[teams.Count];
            for (int i = 0; i < teams.Count; i++)
            {
                TeamProfile team = teams[i];
                if (team.Rank >= 0 && team.Rank < byRank.Length)
                {
                    byRank[team.Rank] = team;
                }
            }

            for (int rank = 0; rank < byRank.Length; rank++)
            {
                if (byRank[rank] == null)
                {
                    byRank[rank] = teams[rank];
                }
            }

            return byRank;
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
