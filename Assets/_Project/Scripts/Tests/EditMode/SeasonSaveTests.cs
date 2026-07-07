using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class SeasonSaveTests
    {
        private const int ClubCount = 20;

        private static League CreateLeague()
        {
            var clubs = new List<Club>(ClubCount);
            for (int i = 0; i < ClubCount; i++)
            {
                double quality = 70.0 - i * (24.0 / (ClubCount - 1));
                clubs.Add(new Club(new ClubId(i), "Club " + (i + 1), new TeamStrength(quality, quality, quality)));
            }

            return new League("Test League", clubs);
        }

        private static MatchSimulator CreateSimulator()
        {
            return new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
        }

        private static MatchContext NormalContext()
        {
            return new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
        }

        [Test]
        public void Capture_SetsCurrentSchemaVersion()
        {
            var season = new LeagueSeason(CreateLeague());

            SeasonSaveData data = new SeasonSaveMapper().Capture(CreateLeague(), season, 0UL);

            Assert.That(data.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
        }

        [Test]
        public void Restore_MidSeason_ReproducesTableAndRound()
        {
            League league = CreateLeague();
            var season = new LeagueSeason(league);
            MatchSimulator simulator = CreateSimulator();
            MatchContext context = NormalContext();
            var rng = new SplitMix64RandomNumberGenerator(2024UL);
            for (int i = 0; i < 10; i++)
            {
                season.AdvanceWeek(simulator, context, rng);
            }

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, season, rng.State);
            RestoredSeason restored = mapper.Restore(data);

            Assert.That(restored.Season.CurrentRound, Is.EqualTo(season.CurrentRound));
            IReadOnlyList<LeagueTableRow> original = season.Table.Ordered();
            IReadOnlyList<LeagueTableRow> reloaded = restored.Season.Table.Ordered();
            Assert.That(reloaded.Count, Is.EqualTo(original.Count));
            for (int i = 0; i < original.Count; i++)
            {
                Assert.That(reloaded[i].Club.Value, Is.EqualTo(original[i].Club.Value));
                Assert.That(reloaded[i].Points, Is.EqualTo(original[i].Points));
                Assert.That(reloaded[i].GoalDifference, Is.EqualTo(original[i].GoalDifference));
            }
        }

        [Test]
        public void SaveRestore_MidSeasonThenContinue_MatchesUninterruptedRun()
        {
            const ulong seed = 777UL;
            MatchSimulator simulator = CreateSimulator();
            MatchContext context = NormalContext();

            // Reference: one uninterrupted run.
            var reference = new LeagueSeason(CreateLeague());
            var referenceRng = new SplitMix64RandomNumberGenerator(seed);
            while (!reference.IsComplete)
            {
                reference.AdvanceWeek(simulator, context, referenceRng);
            }

            // Interrupted: play half, save, restore, continue.
            League league = CreateLeague();
            var season = new LeagueSeason(league);
            var rng = new SplitMix64RandomNumberGenerator(seed);
            int half = season.RoundCount / 2;
            for (int i = 0; i < half; i++)
            {
                season.AdvanceWeek(simulator, context, rng);
            }

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, season, rng.State);
            RestoredSeason restored = mapper.Restore(data);
            var resumedRng = new SplitMix64RandomNumberGenerator(data.RngState);
            while (!restored.Season.IsComplete)
            {
                restored.Season.AdvanceWeek(simulator, context, resumedRng);
            }

            IReadOnlyList<LeagueTableRow> expected = reference.Table.Ordered();
            IReadOnlyList<LeagueTableRow> actual = restored.Season.Table.Ordered();
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].Club.Value, Is.EqualTo(expected[i].Club.Value));
                Assert.That(actual[i].Points, Is.EqualTo(expected[i].Points));
                Assert.That(actual[i].GoalsFor, Is.EqualTo(expected[i].GoalsFor));
            }
        }

        [Test]
        public void Migrate_CurrentVersion_Succeeds()
        {
            var data = new SeasonSaveData { SchemaVersion = SaveSchema.CurrentVersion };

            Result<SeasonSaveData> result = new SaveMigrator().Migrate(data);

            Assert.That(result.IsSuccess, Is.True);
        }

        [Test]
        public void Migrate_NewerVersion_Fails()
        {
            var data = new SeasonSaveData { SchemaVersion = SaveSchema.CurrentVersion + 1 };

            Result<SeasonSaveData> result = new SaveMigrator().Migrate(data);

            Assert.That(result.IsFailure, Is.True);
        }
    }
}
