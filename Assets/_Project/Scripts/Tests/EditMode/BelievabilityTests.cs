using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// League-wide believability regression: simulates a full double round-robin of a spread-quality
    /// league on the pure core and asserts the Gate A bands (goals ~2.5–3, favourites usually win,
    /// home advantage present). Headless, deterministic — the automatable form of the harness check.
    /// </summary>
    public sealed class BelievabilityTests
    {
        private const int TeamCount = 20;
        private const double TopQuality = 70.0;
        private const double BottomQuality = 46.0;

        [Test]
        public void Season_WithDefaultSettings_GoalsAreInBelievableBand()
        {
            SeasonMeasurement measurement = MeasureSeason(MatchSimulationSettings.Default, 20260707UL);

            TestContext.WriteLine($"avg goals/match : {measurement.AverageGoals:F3}");
            TestContext.WriteLine($"favourite win % : {measurement.FavouriteWinPercentage:F1}");
            TestContext.WriteLine($"home win %       : {measurement.HomeWinPercentage:F1}");
            TestContext.WriteLine($"away win %       : {measurement.AwayWinPercentage:F1}");

            Assert.That(measurement.AverageGoals, Is.InRange(2.5, 3.0),
                $"Average goals/match {measurement.AverageGoals:F3} is outside the believable band.");
        }

        [Test]
        public void Season_WithDefaultSettings_FavouriteWinsButUpsetsHappen()
        {
            SeasonMeasurement measurement = MeasureSeason(MatchSimulationSettings.Default, 20260707UL);

            Assert.That(measurement.FavouriteWinPercentage, Is.InRange(45.0, 63.0),
                "Favourite should usually win, but upsets must stay credible.");
        }

        [Test]
        public void Season_WithDefaultSettings_HomeAdvantageIsPresent()
        {
            SeasonMeasurement measurement = MeasureSeason(MatchSimulationSettings.Default, 20260707UL);

            Assert.That(measurement.HomeWinPercentage, Is.GreaterThan(measurement.AwayWinPercentage));
        }

        private static SeasonMeasurement MeasureSeason(MatchSimulationSettings settings, ulong seed)
        {
            var teams = BuildLeague();
            var simulator = new MatchSimulator(new PoissonChanceGenerator(settings), new QualityChanceResolver());
            var context = new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
            var rng = new SplitMix64RandomNumberGenerator(seed);

            int matches = 0;
            int totalGoals = 0;
            int homeWins = 0;
            int awayWins = 0;
            int favouriteWins = 0;

            for (int home = 0; home < teams.Count; home++)
            {
                for (int away = 0; away < teams.Count; away++)
                {
                    if (home == away)
                    {
                        continue;
                    }

                    var command = new MatchCommand(teams[home].Strength, teams[away].Strength, context);
                    MatchOutcome outcome = simulator.Simulate(command, rng);

                    matches++;
                    totalGoals += outcome.HomeGoals + outcome.AwayGoals;

                    if (outcome.HomeGoals > outcome.AwayGoals)
                    {
                        homeWins++;
                    }
                    else if (outcome.AwayGoals > outcome.HomeGoals)
                    {
                        awayWins++;
                    }

                    // Every fixture has a favourite (distinct qualities); denominator is all matches,
                    // so a draw counts against the favourite — matching the harness metric.
                    bool homeIsFavourite = teams[home].Quality > teams[away].Quality;
                    int favouriteGoals = homeIsFavourite ? outcome.HomeGoals : outcome.AwayGoals;
                    int underdogGoals = homeIsFavourite ? outcome.AwayGoals : outcome.HomeGoals;
                    if (favouriteGoals > underdogGoals)
                    {
                        favouriteWins++;
                    }
                }
            }

            return new SeasonMeasurement(
                (double)totalGoals / matches,
                100.0 * favouriteWins / matches,
                100.0 * homeWins / matches,
                100.0 * awayWins / matches);
        }

        private static IReadOnlyList<LeagueEntry> BuildLeague()
        {
            var teams = new List<LeagueEntry>(TeamCount);
            for (int rank = 0; rank < TeamCount; rank++)
            {
                double position = (double)rank / (TeamCount - 1);
                double quality = TopQuality + (BottomQuality - TopQuality) * position;
                teams.Add(new LeagueEntry(quality, new TeamStrength(quality, quality, quality)));
            }

            return teams;
        }

        private sealed class LeagueEntry
        {
            public LeagueEntry(double quality, TeamStrength strength)
            {
                Quality = quality;
                Strength = strength;
            }

            public double Quality { get; }

            public TeamStrength Strength { get; }
        }

        private readonly struct SeasonMeasurement
        {
            public SeasonMeasurement(double averageGoals, double favouriteWinPercentage, double homeWinPercentage, double awayWinPercentage)
            {
                AverageGoals = averageGoals;
                FavouriteWinPercentage = favouriteWinPercentage;
                HomeWinPercentage = homeWinPercentage;
                AwayWinPercentage = awayWinPercentage;
            }

            public double AverageGoals { get; }

            public double FavouriteWinPercentage { get; }

            public double HomeWinPercentage { get; }

            public double AwayWinPercentage { get; }
        }
    }
}
