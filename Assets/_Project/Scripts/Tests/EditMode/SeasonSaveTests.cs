using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
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

            SeasonSaveData data = new SeasonSaveMapper().Capture(CreateLeague(), season, 0UL, 1);

            Assert.That(data.SchemaVersion, Is.EqualTo(SaveSchema.CurrentVersion));
        }

        [Test]
        public void Restore_MidSeason_ReproducesTableAndRound()
        {
            const ulong seed = 2024UL;
            League league = CreateLeague();
            var season = new LeagueSeason(league);
            MatchSimulator simulator = CreateSimulator();
            MatchContext context = NormalContext();
            for (int i = 0; i < 10; i++)
            {
                season.AdvanceWeek(simulator, context, seed);
            }

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, season, seed, 1);
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
            while (!reference.IsComplete)
            {
                reference.AdvanceWeek(simulator, context, seed);
            }

            // Interrupted: play half, save, restore, continue.
            League league = CreateLeague();
            var season = new LeagueSeason(league);
            int half = season.RoundCount / 2;
            for (int i = 0; i < half; i++)
            {
                season.AdvanceWeek(simulator, context, seed);
            }

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, season, seed, 1);
            RestoredSeason restored = mapper.Restore(data);
            while (!restored.Season.IsComplete)
            {
                restored.Season.AdvanceWeek(simulator, context, data.MatchSeed);
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

        // Distinct value in every one of the 29 fields, so a mis-mapped attribute cannot slip through equality.
        private static Attributes DistinctAttributes()
        {
            return new Attributes
            {
                Finishing = 20, Technique = 21, FirstTouch = 22, Dribbling = 23, Passing = 24, Crossing = 25,
                Heading = 26, LongShots = 27, Marking = 28, Tackling = 29, Penalties = 30, FreeKicks = 31,
                Corners = 32, LongThrows = 33, Pace = 34, Acceleration = 35, Stamina = 36, Strength = 37,
                Agility = 38, Jumping = 39, Balance = 40, Positioning = 41, Reflexes = 42, Handling = 43,
                AerialReach = 44, CommandOfArea = 45, OneOnOnes = 46, Kicking = 47, GkPositioning = 48,
            };
        }

        private static League LeagueWithOneSquad()
        {
            var squad = new Squad(new List<Player>
            {
                new Player(new PlayerId(0), "Ada Keeper", "Wales", PlayerRole.Goalkeeper, 29, DistinctAttributes(), 82),
                new Player(new PlayerId(1), "Ben Wolf", "France", PlayerRole.CentreBack, 24, DistinctAttributes(), 88),
                new Player(new PlayerId(2), "Cy Vale", "Spain", PlayerRole.Striker, 19, DistinctAttributes(), 91),
            });
            var withSquad = new Club(new ClubId(0), "Squad Club", squad, new EffectiveStrengthBuilder().Build(squad));
            var strengthOnly = new Club(new ClubId(1), "Strength Club", new TeamStrength(55, 55, 55));
            return new League("Roster League", new List<Club> { withSquad, strengthOnly });
        }

        [Test]
        public void Capture_PersistsSeasonNumber()
        {
            League league = LeagueWithOneSquad();
            var season = new LeagueSeason(league);
            var mapper = new SeasonSaveMapper();

            RestoredSeason restored = mapper.Restore(mapper.Capture(league, season, 5UL, 7));

            Assert.That(restored.SeasonNumber, Is.EqualTo(7));
        }

        [Test]
        public void SaveRestore_Squad_RoundTripsEveryPlayerAndAttribute()
        {
            League league = LeagueWithOneSquad();
            var season = new LeagueSeason(league);
            var mapper = new SeasonSaveMapper();

            RestoredSeason restored = mapper.Restore(mapper.Capture(league, season, 1UL, 1));

            IReadOnlyList<Player> before = league.Clubs[0].Squad.Players;
            Squad restoredSquad = restored.League.Clubs[0].Squad;
            Assert.That(restoredSquad, Is.Not.Null);
            Assert.That(restoredSquad.Players.Count, Is.EqualTo(before.Count));
            for (int i = 0; i < before.Count; i++)
            {
                Player a = before[i];
                Player b = restoredSquad.Players[i];
                Assert.That(b.Id.Value, Is.EqualTo(a.Id.Value));
                Assert.That(b.Name, Is.EqualTo(a.Name));
                Assert.That(b.Nationality, Is.EqualTo(a.Nationality));
                Assert.That(b.Role, Is.EqualTo(a.Role));
                Assert.That(b.Position, Is.EqualTo(a.Position));
                Assert.That(b.Age, Is.EqualTo(a.Age));
                Assert.That(b.HiddenPotential, Is.EqualTo(a.HiddenPotential));
                Assert.That(b.Attributes, Is.EqualTo(a.Attributes));
            }
        }

        [Test]
        public void SaveRestore_StrengthOnlyClub_RestoresWithNullSquad()
        {
            League league = LeagueWithOneSquad();
            var season = new LeagueSeason(league);
            var mapper = new SeasonSaveMapper();

            RestoredSeason restored = mapper.Restore(mapper.Capture(league, season, 1UL, 1));

            Club club = restored.League.Clubs[1];
            Assert.That(club.Squad, Is.Null);
            Assert.That(club.Strength.Attack, Is.EqualTo(55.0).Within(1e-9));
        }

        [Test]
        public void Restore_V2StyleSaveWithoutSquads_RestoresStrengthOnly()
        {
            // An older v2 save has no squads on its clubs; migration + restore must still yield a usable
            // strength-only league rather than crashing on the missing roster.
            var data = new SeasonSaveData
            {
                SchemaVersion = 2,
                LeagueName = "Old Save",
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData { Id = 0, Name = "Legacy", Attack = 60, Midfield = 60, Defence = 60, Squad = null },
                },
            };

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(data);
            RestoredSeason restored = new SeasonSaveMapper().Restore(migrated.Value);

            Assert.That(restored.League.Clubs[0].Squad, Is.Null);
            Assert.That(restored.League.Clubs[0].Strength.Midfield, Is.EqualTo(60.0).Within(1e-9));
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
