using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Tools.SeasonHarness;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Runs the season harness — the project's Gate A instrument — headlessly. The harness used to live in
    /// <c>Gaffer.Editor</c>, so the 1000-season distribution check could only be produced by a human clicking
    /// a button in an EditorWindow; moving the pure files to <c>Gaffer.Tools.SeasonHarness</c> put them in the
    /// <c>dotnet test</c> bridge, and this file is the proof that the instrument now runs in CI.
    /// <para>
    /// It runs a handful of seasons, not a thousand: the point is REACHABILITY and a wiring regression guard,
    /// not the full-precision gate. Deterministic (fixed seeds, injected <see cref="IRandom"/>), so a failure
    /// here means the harness or the sim moved, never that a sample was unlucky. Bands are the harness's own
    /// <see cref="GateCheck"/> thresholds, so they cannot drift apart from what the editor window reports.
    /// </para>
    /// </summary>
    public sealed class SeasonHarnessTests
    {
        private const int Seasons = 4;
        private const int TeamCount = 20;

        private static HarnessReport RunHarness(HarnessConfig config, IReadOnlyList<TeamProfile> teams, IRandom rng)
        {
            var simulator = new MatchSimulator(new PoissonChanceGenerator(MatchSimulationSettings.Default), new QualityChanceResolver());
            var runner = new SeasonRunner(simulator);
            var statistics = new HarnessStatistics(teams.Count);

            for (int season = 0; season < config.SeasonCount; season++)
            {
                runner.RunSeason(teams, rng, statistics);
            }

            return statistics.BuildReport(config, teams);
        }

        private static void AssertGatesAreNotFailing(HarnessReport report)
        {
            foreach (GateCheck check in report.GateChecks)
            {
                Assert.That(check.Status, Is.Not.EqualTo(GateStatus.Fail),
                    $"{check.Label} = {check.Value} ({check.Detail}) — the harness itself calls this a failure.");
            }
        }

        [Test]
        public void Harness_OverAFewSeasons_ProducesABelievableReport()
        {
            var config = new HarnessConfig { SeasonCount = Seasons, TeamCount = TeamCount, Seed = 20260707UL };
            var rng = new SplitMix64RandomNumberGenerator(config.Seed);
            IReadOnlyList<TeamProfile> teams = new LeagueFactory().CreateTeams(config, rng);

            HarnessReport report = RunHarness(config, teams, rng);

            // 20 teams, double round-robin, four seasons.
            Assert.That(report.TotalMatches, Is.EqualTo((long)Seasons * TeamCount * (TeamCount - 1)));
            Assert.That(report.AverageGoalsPerMatch, Is.InRange(2.3, 3.3));
            Assert.That(report.FavouriteWinPercentage, Is.InRange(45.0, 63.0));
            Assert.That(report.HomeWinPercentage, Is.GreaterThan(report.AwayWinPercentage));
            Assert.That(report.SampleTable.Count, Is.EqualTo(TeamCount));
            Assert.That(report.ChampionShares.Count, Is.EqualTo(TeamCount));
            AssertGatesAreNotFailing(report);
        }

        [Test]
        public void Harness_OverAFewSeasons_IsDeterministic()
        {
            HarnessReport first = RunSynthetic(1234UL);
            HarnessReport second = RunSynthetic(1234UL);

            Assert.That(second.AverageGoalsPerMatch, Is.EqualTo(first.AverageGoalsPerMatch));
            Assert.That(second.FavouriteWinPercentage, Is.EqualTo(first.FavouriteWinPercentage));
            Assert.That(second.HomeWinPercentage, Is.EqualTo(first.HomeWinPercentage));
            for (int i = 0; i < first.SampleTable.Count; i++)
            {
                Assert.That(second.SampleTable[i].Name, Is.EqualTo(first.SampleTable[i].Name));
                Assert.That(second.SampleTable[i].Points, Is.EqualTo(first.SampleTable[i].Points));
            }
        }

        [Test]
        public void Harness_HtmlExport_RendersTheWholeReport()
        {
            // HtmlReportWriter is pure string building, so the export path the editor window offers is now
            // checked headless too — it used to be reachable only through a file dialog.
            HarnessReport report = RunSynthetic(20260707UL);

            string html = new HtmlReportWriter().Render(report);

            Assert.That(html, Does.StartWith("<title>GAFFER - Season Harness</title>"));
            Assert.That(html, Does.Contain("A sample final table"));
            Assert.That(html, Does.Contain(report.SampleTable[0].Name), "the champion is named in the rendered table");
            Assert.That(html, Does.Contain("Favourite win rate"), "every gate check reaches the export");
        }

        /// <summary>
        /// The measurement <c>BelievabilityTests</c> cannot make: it builds synthetic <see cref="TeamStrength"/>
        /// values directly, so the squad→strength path a played season actually uses
        /// (<c>PlayerGenerator</c> → <c>SquadGenerator</c> → <c>EffectiveStrengthBuilder</c>) is never
        /// exercised by the distribution gate. Here the league is GENERATED — every team's strength is derived
        /// from real rosters through the shipped builder — and the same harness statistics are applied to it,
        /// so a change to ratings, role weights or the strength builder that pushes the league out of the
        /// believable band fails here rather than in a play session.
        /// </summary>
        [Test]
        public void Harness_WithGeneratedSquads_KeepsTheBelievableBands()
        {
            var config = new HarnessConfig { SeasonCount = Seasons, TeamCount = TeamCount, Seed = 20260707UL };
            var rng = new SplitMix64RandomNumberGenerator(config.Seed);
            IReadOnlyList<TeamProfile> teams = GenerateTeamsFromSquads(config.TeamCount, rng);

            HarnessReport report = RunHarness(config, teams, rng);

            Assert.That(report.AverageGoalsPerMatch, Is.InRange(2.3, 3.3),
                $"squad-derived league: {report.AverageGoalsPerMatch:F3} goals/match.");
            Assert.That(report.FavouriteWinPercentage, Is.InRange(45.0, 63.0),
                $"squad-derived league: favourite won {report.FavouriteWinPercentage:F1}%.");
            Assert.That(report.HomeWinPercentage, Is.GreaterThan(report.AwayWinPercentage));
            AssertGatesAreNotFailing(report);
        }

        /// <summary>
        /// The regression that made "titles by pre-season rank" a lie for 1000 seasons at a stretch: the
        /// rank used to come from the quality curve alone, but the per-axis jitter is bigger than one step
        /// of that curve, so rank #2 could be handed the strongest squad in the league. Because the teams
        /// are built ONCE and replayed every season, that single draw showed up as a permanent "the second
        /// favourite wins 65% more titles than the first" — a sim bias that was really a label.
        /// </summary>
        [Test]
        public void CreateTeams_RanksByTheStrengthTheSimIsHanded_NotByTheQualityCurve()
        {
            var config = new HarnessConfig { SeasonCount = Seasons, TeamCount = TeamCount, Seed = 20260707UL };

            IReadOnlyList<TeamProfile> teams = new LeagueFactory().CreateTeams(config, new SplitMix64RandomNumberGenerator(config.Seed));

            for (int rank = 1; rank < teams.Count; rank++)
            {
                Assert.That(Overall(teams[rank].Strength), Is.LessThanOrEqualTo(Overall(teams[rank - 1].Strength)),
                    $"rank #{rank + 1} was handed a stronger squad than rank #{rank}.");
                Assert.That(teams[rank].Rank, Is.EqualTo(rank), "the list is in rank order, which the report indexes by.");
            }
        }

        /// <summary>
        /// The harness's own gate on the above, so a future league builder that ranks by anything but
        /// strength fails the run instead of quietly producing a mislabelled title table.
        /// </summary>
        [Test]
        public void Report_WhenARankIsStrongerThanTheOneAboveIt_FailsTheRankOrderGate()
        {
            var config = new HarnessConfig { SeasonCount = 1, TeamCount = 2, Seed = 7UL };
            var teams = new List<TeamProfile>
            {
                new TeamProfile(0, "Weaker", 50.0, new TeamStrength(50.0, 50.0, 50.0)),
                new TeamProfile(1, "Stronger", 70.0, new TeamStrength(70.0, 70.0, 70.0)),
            };
            var statistics = new HarnessStatistics(teams.Count);
            new SeasonRunner(new MatchSimulator(new PoissonChanceGenerator(MatchSimulationSettings.Default), new QualityChanceResolver()))
                .RunSeason(teams, new SplitMix64RandomNumberGenerator(config.Seed), statistics);

            HarnessReport report = statistics.BuildReport(config, teams);

            GateCheck rankOrder = null;
            foreach (GateCheck check in report.GateChecks)
            {
                if (check.Label == "Rank order")
                {
                    rankOrder = check;
                }
            }

            Assert.That(rankOrder, Is.Not.Null, "the report carries a rank-order gate.");
            Assert.That(rankOrder.Status, Is.EqualTo(GateStatus.Fail));
            Assert.That(rankOrder.Value, Does.Contain("1"), "the one inverted pair is counted.");
        }

        /// <summary>
        /// Titles are counted by <c>TeamProfile.Rank</c> but the row used to be NAMED by list position, so
        /// a team list in any other order would have printed one club's name against another's titles.
        /// </summary>
        [Test]
        public void Report_WhenTheTeamListIsNotInRankOrder_StillNamesEachRanksOwnClub()
        {
            var config = new HarnessConfig { SeasonCount = 1, TeamCount = 2, Seed = 7UL };
            var teams = new List<TeamProfile>
            {
                new TeamProfile(1, "Second", 50.0, new TeamStrength(50.0, 50.0, 50.0)),
                new TeamProfile(0, "First", 70.0, new TeamStrength(70.0, 70.0, 70.0)),
            };
            var statistics = new HarnessStatistics(teams.Count);
            new SeasonRunner(new MatchSimulator(new PoissonChanceGenerator(MatchSimulationSettings.Default), new QualityChanceResolver()))
                .RunSeason(teams, new SplitMix64RandomNumberGenerator(config.Seed), statistics);

            HarnessReport report = statistics.BuildReport(config, teams);

            Assert.That(report.ChampionShares[0].Name, Is.EqualTo("First"));
            Assert.That(report.ChampionShares[1].Name, Is.EqualTo("Second"));
        }

        private static HarnessReport RunSynthetic(ulong seed)
        {
            var config = new HarnessConfig { SeasonCount = Seasons, TeamCount = TeamCount, Seed = seed };
            var rng = new SplitMix64RandomNumberGenerator(seed);
            IReadOnlyList<TeamProfile> teams = new LeagueFactory().CreateTeams(config, rng);
            return RunHarness(config, teams, rng);
        }

        /// <summary>
        /// Builds the harness's team profiles from a generated league instead of a quality curve: the rank is
        /// the club's own squad strength order, and <see cref="TeamProfile.BaseQuality"/> — which is what the
        /// statistics use to decide who the favourite was — is the club's overall derived strength, so
        /// "favourite" means "the better squad" and not "the number we asked for".
        /// </summary>
        private static IReadOnlyList<TeamProfile> GenerateTeamsFromSquads(int teamCount, IRandom rng)
        {
            League league = new LeagueGenerator(new SquadGenerator(new PlayerGenerator())).Generate(teamCount, rng);

            var ordered = new List<Club>(league.Clubs);
            ordered.Sort((a, b) => Overall(b.Strength).CompareTo(Overall(a.Strength)));

            var teams = new List<TeamProfile>(teamCount);
            for (int rank = 0; rank < ordered.Count; rank++)
            {
                Club club = ordered[rank];
                teams.Add(new TeamProfile(rank, club.Name, Overall(club.Strength), club.Strength));
            }

            return teams;
        }

        private static double Overall(TeamStrength strength)
        {
            return (strength.Attack + strength.Midfield + strength.Defence) / 3.0;
        }
    }
}
