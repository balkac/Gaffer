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

        // The season owns its simulator (ARCHITECTURE §6).
        private static LeagueSeason PlayableSeason(League league)
        {
            return new LeagueSeason(league, null, null, null, CreateSimulator());
        }

        // What RunSessionFactory.Resume does in production: the mapper hands back the league, the round and
        // the result history as data, and the caller — which is the side that owns a simulator — builds the
        // playable season from them. This helper is the same call RunSession makes, so a test that resumes
        // through it is exercising the production resume path, not a test-only shortcut.
        private static LeagueSeason Resume(RestoredSeason restored)
        {
            return LeagueSeason.Restore(
                restored.League,
                restored.PlayedRounds,
                restored.PlayedResults,
                null, null, null,
                CreateSimulator());
        }

        [Test]
        public void Capture_SetsCurrentSchemaVersion()
        {
            var season = PlayableSeason(CreateLeague());

            SeasonSaveData data = new SeasonSaveMapper().Capture(CreateLeague(), season, 0UL, 1);

            // The literal, not SeasonSaveData.CurrentVersion: comparing what Capture writes against the
            // constant Capture writes agrees with itself at every version and would go on passing through
            // a bump that shipped without a migration. Spelled out, bumping the schema is a deliberate
            // act that fails here until the number and its migration are both updated.
            Assert.That(data.SchemaVersion, Is.EqualTo(5));
            Assert.That(SeasonSaveData.CurrentVersion, Is.EqualTo(5),
                "The schema version moved. Add the migration step, then update this test.");
        }

        [Test]
        public void Restore_MidSeason_ReproducesTableAndRound()
        {
            const ulong seed = 2024UL;
            League league = CreateLeague();
            var season = PlayableSeason(league);
            MatchSimulator simulator = CreateSimulator();
            MatchContext context = NormalContext();
            for (int i = 0; i < 10; i++)
            {
                season.AdvanceWeek(context, seed);
            }

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, season, seed, 1);
            RestoredSeason restored = mapper.Restore(data);

            // Asserted on the season the run would actually carry on with — rebuilt from the restored data
            // the way RunSession rebuilds it — rather than on a carrier object the production path throws
            // away. Same table, and now it is the table someone can keep playing.
            LeagueSeason resumed = Resume(restored);

            Assert.That(restored.PlayedRounds, Is.EqualTo(season.CurrentRound));
            Assert.That(resumed.CurrentRound, Is.EqualTo(season.CurrentRound));
            IReadOnlyList<LeagueTableRow> original = season.Table.Ordered();
            IReadOnlyList<LeagueTableRow> reloaded = resumed.Table.Ordered();
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
            var reference = PlayableSeason(CreateLeague());
            while (!reference.IsComplete)
            {
                reference.AdvanceWeek(context, seed);
            }

            // Interrupted: play half, save, restore, continue.
            League league = CreateLeague();
            var season = PlayableSeason(league);
            int half = season.RoundCount / 2;
            for (int i = 0; i < half; i++)
            {
                season.AdvanceWeek(context, seed);
            }

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, season, seed, 1);
            RestoredSeason restored = mapper.Restore(data);
            LeagueSeason resumed = Resume(restored);
            while (!resumed.IsComplete)
            {
                resumed.AdvanceWeek(context, data.MatchSeed);
            }

            IReadOnlyList<LeagueTableRow> expected = reference.Table.Ordered();
            IReadOnlyList<LeagueTableRow> actual = resumed.Table.Ordered();
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
                Finishing = 20,
                Technique = 21,
                FirstTouch = 22,
                Dribbling = 23,
                Passing = 24,
                Crossing = 25,
                Heading = 26,
                LongShots = 27,
                Marking = 28,
                Tackling = 29,
                Penalties = 30,
                FreeKicks = 31,
                Corners = 32,
                LongThrows = 33,
                Pace = 34,
                Acceleration = 35,
                Stamina = 36,
                Strength = 37,
                Agility = 38,
                Jumping = 39,
                Balance = 40,
                Positioning = 41,
                Reflexes = 42,
                Handling = 43,
                AerialReach = 44,
                CommandOfArea = 45,
                OneOnOnes = 46,
                Kicking = 47,
                GkPositioning = 48,
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
            var season = PlayableSeason(league);
            var mapper = new SeasonSaveMapper();

            RestoredSeason restored = mapper.Restore(mapper.Capture(league, season, 5UL, 7));

            Assert.That(restored.SeasonNumber, Is.EqualTo(7));
        }

        [Test]
        public void SaveRestore_Squad_RoundTripsEveryPlayerAndAttribute()
        {
            League league = LeagueWithOneSquad();
            var season = PlayableSeason(league);
            var mapper = new SeasonSaveMapper();

            SeasonSaveData data = mapper.Capture(league, season, 1UL, 1);
            RestoredSeason restored = mapper.Restore(data);

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

            // The loop above compares the role against a value that travelled through the same representation
            // in both directions, so it agrees with itself no matter what that representation is — it would
            // pass just as happily on the ordinal scheme that could re-role a whole save. These assertions
            // name the expected roles as literals, and name what the document itself has to contain.
            Assert.That(restoredSquad.Players[0].Role, Is.EqualTo(PlayerRole.Goalkeeper));
            Assert.That(restoredSquad.Players[1].Role, Is.EqualTo(PlayerRole.CentreBack));
            Assert.That(restoredSquad.Players[2].Role, Is.EqualTo(PlayerRole.Striker));

            List<PlayerSaveData> saved = data.Clubs[0].Squad;
            Assert.That(saved[0].RoleName, Is.EqualTo("Goalkeeper"), "the save must carry the role BY NAME (v5)");
            Assert.That(saved[1].RoleName, Is.EqualTo("CentreBack"));
            Assert.That(saved[2].RoleName, Is.EqualTo("Striker"));
            Assert.That(saved[2].Role, Is.Null, "a v5 save must not write the retired ordinal field at all");
        }

        [Test]
        public void SaveRestore_StrengthOnlyClub_RestoresWithNullSquad()
        {
            League league = LeagueWithOneSquad();
            var season = PlayableSeason(league);
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
            var data = new SeasonSaveData { SchemaVersion = SeasonSaveData.CurrentVersion };

            Result<SeasonSaveData> result = new SaveMigrator().Migrate(data);

            Assert.That(result.IsSuccess, Is.True);
        }

        /// <summary>A genuine v4 document: the role is the raw <c>PlayerRole</c> ordinal and there is no
        /// <c>RoleName</c> at all, exactly as it sits in a file already on a player's device.</summary>
        private static SeasonSaveData CreateV4Save(int roleOrdinal)
        {
            return new SeasonSaveData
            {
                SchemaVersion = 4,
                LeagueName = "V4 League",
                SeasonNumber = 2,
                MatchSeed = 42UL,
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData
                    {
                        Id = 0, Name = "Old FC", Attack = 60, Midfield = 60, Defence = 60,
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData
                            {
                                Id = 1, Name = "Old Player", Nationality = "England",
                                Role = roleOrdinal, RoleName = null,
                                Age = 24, HiddenPotential = 70, Attributes = new AttributesSaveData(),
                            },
                        },
                    },
                },
            };
        }

        [Test]
        public void Migrate_V4OrdinalRole_BecomesTheRoleName()
        {
            // 11 is what a v4 save wrote for a striker. The migration must turn that number into the name,
            // and the restored player must be a striker BY NAME — not "whatever role sits at 11 today".
            SeasonSaveData data = CreateV4Save((int)PlayerRole.Striker);

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(data);

            Assert.That(migrated.IsSuccess, Is.True, migrated.Error);
            PlayerSaveData player = migrated.Value.Clubs[0].Squad[0];
            Assert.That(player.RoleName, Is.EqualTo("Striker"));
            Assert.That(player.Role, Is.Null, "the migration retires the ordinal field as it converts it");
            Assert.That(migrated.Value.SchemaVersion, Is.EqualTo(SeasonSaveData.CurrentVersion));

            RestoredSeason restored = new SeasonSaveMapper().Restore(migrated.Value);
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Role, Is.EqualTo(PlayerRole.Striker));
        }

        [Test]
        public void Migrate_V4Save_RunTwice_IsIdempotent()
        {
            SeasonSaveData data = CreateV4Save((int)PlayerRole.CentreBack);
            var migrator = new SaveMigrator();

            Result<SeasonSaveData> once = migrator.Migrate(data);
            Result<SeasonSaveData> twice = migrator.Migrate(once.Value);

            Assert.That(twice.IsSuccess, Is.True, twice.Error);
            Assert.That(twice.Value.Clubs[0].Squad[0].RoleName, Is.EqualTo("CentreBack"));
        }

        [Test]
        public void Migrate_V4SaveWithAnUndefinedRoleOrdinal_FailsInsteadOfGuessing()
        {
            SeasonSaveData data = CreateV4Save(99);

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(data);

            Assert.That(migrated.IsFailure, Is.True, "an ordinal outside the enum means a corrupt document");
            Assert.That(migrated.Error, Does.Contain("99"));
        }

        [Test]
        public void Migrate_SaveWithAnUnknownRoleName_IsAResultFailureNotASilentDefault()
        {
            SeasonSaveData data = CreateV4Save((int)PlayerRole.Striker);
            data.SchemaVersion = SeasonSaveData.CurrentVersion;
            data.Clubs[0].Squad[0].Role = null;
            data.Clubs[0].Squad[0].RoleName = "SweeperKeeper";

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(data);

            Assert.That(migrated.IsFailure, Is.True);
            Assert.That(migrated.Error, Does.Contain("SweeperKeeper"));
        }

        [Test]
        public void Migrate_SaveWhoseRoleNameIsANumber_IsRejected()
        {
            // Enum.TryParse alone returns true for a numeric string and yields an undefined value, which is
            // why the read pairs it with Enum.IsDefined (CONVENTIONS §6). A hand-edited save is the realistic
            // way "11" ends up in the name field.
            SeasonSaveData data = CreateV4Save((int)PlayerRole.Striker);
            data.SchemaVersion = SeasonSaveData.CurrentVersion;
            data.Clubs[0].Squad[0].Role = null;
            data.Clubs[0].Squad[0].RoleName = "999";

            Assert.That(new SaveMigrator().Migrate(data).IsFailure, Is.True);
        }

        [Test]
        public void PersistedPlayerRole_ReadsWhatItWrote_AndRejectsEverythingElse()
        {
            Assert.That(PersistedPlayerRole.ToName(PlayerRole.AttackingMidfield), Is.EqualTo("AttackingMidfield"));

            Assert.That(PersistedPlayerRole.TryParse("AttackingMidfield", out PlayerRole role), Is.True);
            Assert.That(role, Is.EqualTo(PlayerRole.AttackingMidfield));

            Assert.That(PersistedPlayerRole.TryParse("999", out PlayerRole _), Is.False, "a numeric string is not a member");
            Assert.That(PersistedPlayerRole.TryParse("11", out PlayerRole _), Is.False, "not even a numeric string that IS a valid ordinal");
            // Enum.TryParse also accepts a comma-separated list on a non-flags enum and returns the bitwise
            // combination: 8 | 4 = 12, which is no role at all.
            Assert.That(PersistedPlayerRole.TryParse("LeftMidfield, DefensiveMidfield", out PlayerRole _), Is.False, "nor a combination");
            Assert.That(PersistedPlayerRole.TryParse("striker", out PlayerRole _), Is.False, "the persisted name is a machine string, matched exactly");
            Assert.That(PersistedPlayerRole.TryParse(null, out PlayerRole _), Is.False);
            Assert.That(PersistedPlayerRole.TryParse(string.Empty, out PlayerRole _), Is.False);
        }

        [Test]
        public void Migrate_NewerVersion_Fails()
        {
            var data = new SeasonSaveData { SchemaVersion = SeasonSaveData.CurrentVersion + 1 };

            Result<SeasonSaveData> result = new SaveMigrator().Migrate(data);

            Assert.That(result.IsFailure, Is.True);
        }
    }
}
