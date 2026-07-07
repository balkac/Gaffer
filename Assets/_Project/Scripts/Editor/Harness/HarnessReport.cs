using System.Collections.Generic;

namespace Gaffer.Editor.Harness
{
    // One immutable payload the writers render — grouped DTOs (CONVENTIONS §2 payload exception).

    public enum GateStatus
    {
        Pass,
        Warn,
        Fail
    }

    public sealed class GateCheck
    {
        public GateCheck(string label, string value, string detail, GateStatus status)
        {
            Label = label;
            Value = value;
            Detail = detail;
            Status = status;
        }

        public string Label { get; }

        public string Value { get; }

        public string Detail { get; }

        public GateStatus Status { get; }
    }

    public sealed class HistogramBin
    {
        public HistogramBin(string label, long count, double percentage)
        {
            Label = label;
            Count = count;
            Percentage = percentage;
        }

        public string Label { get; }

        public long Count { get; }

        public double Percentage { get; }
    }

    public sealed class ChampionShare
    {
        public ChampionShare(int rank, string name, long titles, double percentage)
        {
            Rank = rank;
            Name = name;
            Titles = titles;
            Percentage = percentage;
        }

        public int Rank { get; }

        public string Name { get; }

        public long Titles { get; }

        public double Percentage { get; }
    }

    public sealed class TableRowView
    {
        public TableRowView(int position, TeamProfile team, StandingsRow row)
        {
            Position = position;
            Name = team.Name;
            PreSeasonRank = team.Rank;
            Played = row.Played;
            Won = row.Won;
            Drawn = row.Drawn;
            Lost = row.Lost;
            GoalsFor = row.GoalsFor;
            GoalsAgainst = row.GoalsAgainst;
            GoalDifference = row.GoalDifference;
            Points = row.Points;
        }

        public int Position { get; }

        public string Name { get; }

        public int PreSeasonRank { get; }

        public int Played { get; }

        public int Won { get; }

        public int Drawn { get; }

        public int Lost { get; }

        public int GoalsFor { get; }

        public int GoalsAgainst { get; }

        public int GoalDifference { get; }

        public int Points { get; }
    }

    public sealed class HarnessReport
    {
        public HarnessReport(
            HarnessConfig config,
            long totalMatches,
            double averageGoalsPerMatch,
            IReadOnlyList<HistogramBin> goalBins,
            double homeWinPercentage,
            double drawPercentage,
            double awayWinPercentage,
            double favouriteWinPercentage,
            double favouriteDrawPercentage,
            double favouriteUpsetPercentage,
            IReadOnlyList<ChampionShare> championShares,
            IReadOnlyList<TableRowView> sampleTable,
            IReadOnlyList<GateCheck> gateChecks)
        {
            Config = config;
            TotalMatches = totalMatches;
            AverageGoalsPerMatch = averageGoalsPerMatch;
            GoalBins = goalBins;
            HomeWinPercentage = homeWinPercentage;
            DrawPercentage = drawPercentage;
            AwayWinPercentage = awayWinPercentage;
            FavouriteWinPercentage = favouriteWinPercentage;
            FavouriteDrawPercentage = favouriteDrawPercentage;
            FavouriteUpsetPercentage = favouriteUpsetPercentage;
            ChampionShares = championShares;
            SampleTable = sampleTable;
            GateChecks = gateChecks;
        }

        public HarnessConfig Config { get; }

        public long TotalMatches { get; }

        public double AverageGoalsPerMatch { get; }

        public IReadOnlyList<HistogramBin> GoalBins { get; }

        public double HomeWinPercentage { get; }

        public double DrawPercentage { get; }

        public double AwayWinPercentage { get; }

        public double FavouriteWinPercentage { get; }

        public double FavouriteDrawPercentage { get; }

        public double FavouriteUpsetPercentage { get; }

        public IReadOnlyList<ChampionShare> ChampionShares { get; }

        public IReadOnlyList<TableRowView> SampleTable { get; }

        public IReadOnlyList<GateCheck> GateChecks { get; }
    }
}
