using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
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

        /// <summary>
        /// The Gate A seeds. The gate used to rest on <c>20260707</c> alone while the harness it stands in
        /// for runs a thousand seasons — one sample cannot tell a calibrated sim from a lucky one. These
        /// five were measured together and none was chosen for passing; the numbers each produces are in
        /// the table below and in <see cref="Season_WithDefaultSettings_MeasurementsAreOnTheRecord"/>.
        /// </summary>
        private static readonly ulong[] GateASeeds = { 20260707UL, 1UL, 42UL, 20250101UL, 987654321UL };

        [Test]
        public void Season_WithDefaultSettings_GoalsAreInBelievableBand()
        {
            foreach (ulong seed in GateASeeds)
            {
                SeasonMeasurement measurement = MeasureSeason(MatchSimulationSettings.Default, seed);

                Assert.That(measurement.AverageGoals, Is.InRange(2.5, 3.0),
                    $"seed {seed}: average goals/match {measurement.AverageGoals:F3} is outside the believable band.");
            }
        }

        [Test]
        public void Season_WithDefaultSettings_FavouriteWinsButUpsetsHappen()
        {
            foreach (ulong seed in GateASeeds)
            {
                SeasonMeasurement measurement = MeasureSeason(MatchSimulationSettings.Default, seed);

                Assert.That(measurement.FavouriteWinPercentage, Is.InRange(45.0, 63.0),
                    $"seed {seed}: favourite won {measurement.FavouriteWinPercentage:F1}% — should usually win, " +
                    "but upsets must stay credible.");
            }
        }

        [Test]
        public void Season_WithDefaultSettings_HomeAdvantageIsPresentAcrossTheSweep()
        {
            // Asserted over the POOLED sweep, not per season, and that is a correction rather than a
            // loosening. One season is 380 matches, so a home-win share near 38% carries a standard error
            // around 2.5 points; two shares that differ by ~3 points can and do invert on a single sample.
            // They do here: on seed 20250101 the same calibrated sim returns home 36.3% against away
            // 36.8%. The per-season assertion this replaces would have failed on that seed — which says
            // the claim was mis-specified, not that home advantage is missing. Home advantage is a
            // property of the population, so it is measured on 1,900 matches, where the margin is
            // decisive (41.7% vs 34.2%). The per-seed figures stay visible in
            // Season_WithDefaultSettings_MeasurementsAreOnTheRecord.
            double totalHome = 0.0;
            double totalAway = 0.0;
            foreach (ulong seed in GateASeeds)
            {
                SeasonMeasurement measurement = MeasureSeason(MatchSimulationSettings.Default, seed);
                totalHome += measurement.HomeWinPercentage;
                totalAway += measurement.AwayWinPercentage;
            }

            double home = totalHome / GateASeeds.Length;
            double away = totalAway / GateASeeds.Length;

            Assert.That(home, Is.GreaterThan(away),
                $"Across {GateASeeds.Length} seasons home won {home:F1}% and away {away:F1}% — no home advantage.");
            Assert.That(home - away, Is.GreaterThan(3.0),
                $"Home advantage measured {home - away:F1} points across the sweep; below ~3 it is inside one " +
                "season's noise and cannot be told apart from none.");
        }

        [Test]
        public void Season_WithDefaultSettings_MeasurementsAreOnTheRecord()
        {
            // Not a band — the sweep's actual numbers, written to the test log so a calibration change is
            // reviewed against evidence rather than against whether an assertion happened to hold.
            // Measured on this baseline:
            //   seed 20260707 : goals 2.692 · fav 51.6% · home 38.2% · away 35.8%
            //   seed 1        : goals 2.550 · fav 49.2% · home 43.4% · away 33.2%
            //   seed 42       : goals 2.755 · fav 48.7% · home 48.2% · away 29.7%
            //   seed 20250101 : goals 2.671 · fav 51.3% · home 36.3% · away 36.8%   <-- home advantage inverts
            //   seed 987654321: goals 2.692 · fav 49.2% · home 42.6% · away 35.3%
            foreach (ulong seed in GateASeeds)
            {
                SeasonMeasurement m = MeasureSeason(MatchSimulationSettings.Default, seed);
                TestContext.WriteLine(
                    $"seed {seed,-10} goals {m.AverageGoals:F3} · fav {m.FavouriteWinPercentage:F1}% · " +
                    $"home {m.HomeWinPercentage:F1}% · away {m.AwayWinPercentage:F1}%");
            }

            // The original single-seed gate, pinned exactly, so the sweep above cannot quietly replace the
            // number this project has been calibrated against.
            SeasonMeasurement pinned = MeasureSeason(MatchSimulationSettings.Default, 20260707UL);
            Assert.That(pinned.AverageGoals, Is.EqualTo(2.692).Within(0.001));
            Assert.That(pinned.FavouriteWinPercentage, Is.EqualTo(51.6).Within(0.1));
            Assert.That(pinned.HomeWinPercentage, Is.EqualTo(38.2).Within(0.1));
            Assert.That(pinned.AwayWinPercentage, Is.EqualTo(35.8).Within(0.1));
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
