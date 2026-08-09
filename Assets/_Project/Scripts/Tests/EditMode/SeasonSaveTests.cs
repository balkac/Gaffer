using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
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
            Assert.That(data.SchemaVersion, Is.EqualTo(6));
            Assert.That(SeasonSaveData.CurrentVersion, Is.EqualTo(6),
                "The schema version moved. Add the migration step, then update this test.");
        }

        // ----- v5 -> v6: the document grows a run block ---------------------------------------------------

        /// <summary>A genuine v5 document: the season and nothing around it, exactly as it sits in a file
        /// the owner already has.</summary>
        private static SeasonSaveData CreateV5Save()
        {
            return new SeasonSaveData
            {
                SchemaVersion = 5,
                LeagueName = "V5 League",
                SeasonNumber = 3,
                PlayedRounds = 11,
                MatchSeed = 20260709UL,
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData
                    {
                        Id = 0, Name = "Old FC", Attack = 60, Midfield = 60, Defence = 60,
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData
                            {
                                Id = 1, Name = "Old Player", Nationality = "England", RoleName = "Striker",
                                Age = 24, HiddenPotential = 70, Attributes = new AttributesSaveData(),
                            },
                        },
                    },
                },
            };
        }

        [Test]
        public void Migrate_V5Save_GainsARunBlockCarryingTheOnlyRunFactItHas()
        {
            // The step fills ONE field and guesses at nothing else. A v5 document records exactly one thing
            // about the run as a run — the seed — and that seed IS the original one, because before v6
            // nothing ever re-rolled it. Everything else is left absent, which is how the resume path is
            // told to fall back to the caller's setup: i.e. to behave exactly as a v5 load already did.
            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(CreateV5Save());

            Assert.That(migrated.IsSuccess, Is.True, migrated.Error);
            Assert.That(migrated.Value.SchemaVersion, Is.EqualTo(SeasonSaveData.CurrentVersion));

            RunSaveData run = migrated.Value.Run;
            Assert.That(run, Is.Not.Null, "a v6 document always has a run block");
            Assert.That(run.OriginalSeed, Is.EqualTo(20260709UL), "the v5 match seed is the run's original seed");

            Assert.That(run.Setup, Is.Null, "a v5 save never recorded which club was managed — the caller's setup stands");
            Assert.That(run.Finances, Is.Null, "nor the money");
            Assert.That(run.Tactics, Is.Null, "nor the shape and tactics");
            Assert.That(run.Eleven, Is.Null, "nor the team sheet");
            Assert.That(run.Market, Is.Null, "nor the market");
            Assert.That(run.Morale, Is.Null, "nor live morale");
            Assert.That(run.Drama, Is.Null, "nor the drama engine's memory");

            // And the season it always carried is untouched: no field changed meaning on the way through.
            Assert.That(migrated.Value.MatchSeed, Is.EqualTo(20260709UL));
            Assert.That(migrated.Value.PlayedRounds, Is.EqualTo(11));
            Assert.That(migrated.Value.SeasonNumber, Is.EqualTo(3));
            Assert.That(migrated.Value.Clubs[0].Squad[0].RoleName, Is.EqualTo("Striker"));
        }

        [Test]
        public void Migrate_V5Save_RunTwice_IsIdempotent()
        {
            var migrator = new SaveMigrator();
            SeasonSaveData once = migrator.Migrate(CreateV5Save()).Value;
            once.Run.Finances = new FinancesSaveData { Cash = 42L };

            Result<SeasonSaveData> twice = migrator.Migrate(once);

            Assert.That(twice.IsSuccess, Is.True, twice.Error);
            Assert.That(twice.Value.Run.Finances.Cash, Is.EqualTo(42L),
                "a second pass must not replace a block that is already there");
        }

        [Test]
        public void Migrate_V2SaveWithNoSquads_StillReachesV6()
        {
            // The chain end to end: the oldest document the build accepts falls through every step in order.
            var data = new SeasonSaveData
            {
                SchemaVersion = 2,
                LeagueName = "Ancient",
                MatchSeed = 7UL,
                Clubs = new List<ClubSaveData> { new ClubSaveData { Id = 0, Name = "Legacy", Squad = null } },
            };

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(data);

            Assert.That(migrated.IsSuccess, Is.True, migrated.Error);
            Assert.That(migrated.Value.SchemaVersion, Is.EqualTo(SeasonSaveData.CurrentVersion));
            Assert.That(migrated.Value.Run.OriginalSeed, Is.EqualTo(7UL));
        }

        [Test]
        public void Migrate_ARunBlockWhoseMarketHasAnUnknownRole_IsAResultFailure()
        {
            // The market is a roster, so it passes the same gate the squads do. Without this a hand-edited
            // prospect would slip past migration and throw inside Restore, which is the one place a bad file
            // must never reach.
            SeasonSaveData data = CreateV5Save();
            data.SchemaVersion = SeasonSaveData.CurrentVersion;
            data.Run = new RunSaveData
            {
                Market = new List<PlayerSaveData>
                {
                    new PlayerSaveData { Id = 900, Name = "Ghost", RoleName = "SweeperKeeper" },
                },
            };

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(data);

            Assert.That(migrated.IsFailure, Is.True);
            Assert.That(migrated.Error, Does.Contain("SweeperKeeper").And.Contains("market"));
        }

        // ----- The run block, through the mapper ----------------------------------------------------------

        [Test]
        public void SaveRestore_RunBlock_RoundTripsThroughTheMapper()
        {
            // The mapper's own half of the round trip: domain in, document out, domain back — including the
            // two enum families that now cross the boundary, which travel BY NAME.
            League league = LeagueWithOneSquad();
            var season = PlayableSeason(league);
            var mapper = new SeasonSaveMapper();
            var run = new RunState(
                originalSeed: 99UL,
                setup: new RunSetupState(1, 2, 3, 4, 5, 6),
                finances: new Finances(1_000L, 2_000L, 1_500L),
                formation: Formation.F352,
                tactics: new Tactics(Mentality.VeryDefensive, Tempo.Intense, Pressing.Press, Approach.Possession),
                eleven: new[] { 0, 1, 2, -1 },
                market: new List<Player> { league.Clubs[0].Squad.Players[2] },
                morale: new List<MoraleEntry> { new MoraleEntry(new PlayerId(1), -2.5, 3) },
                drama: new DramaEngineState(9, 7, 2, new List<DramaEventMark> { new DramaEventMark(new DramaEventId("fan-protest"), 7) }),
                pendingEvent: new DramaEventId("budget-cut"),
                pendingSubjectPlayerId: 1);

            SeasonSaveData data = mapper.Capture(league, season, 5UL, 1, run);

            Assert.That(data.Run.Tactics.Mentality, Is.EqualTo("VeryDefensive"), "the save must carry the axis BY NAME");
            Assert.That(data.Run.Tactics.FormationSlots[0], Is.EqualTo("Goalkeeper"));

            RunState back = mapper.Restore(data).Run;

            Assert.That(back.OriginalSeed, Is.EqualTo(99UL));
            Assert.That(back.Setup.ManagedClubIndex, Is.EqualTo(1));
            Assert.That(back.Setup.GuaranteedGems, Is.EqualTo(6));
            Assert.That(back.Finances.Value.Cash, Is.EqualTo(1_000L));
            Assert.That(back.Finances.Value.WeeklyWageBudget, Is.EqualTo(2_000L));
            Assert.That(back.Finances.Value.WeeklyWageBill, Is.EqualTo(1_500L));
            Assert.That(back.Formation.Value.Name, Is.EqualTo("3-5-2"));
            Assert.That(back.Formation.Value.Slots, Is.EqualTo(Formation.F352.Slots));
            Assert.That(back.Tactics.Value.Mentality, Is.EqualTo(Mentality.VeryDefensive));
            Assert.That(back.Tactics.Value.Tempo, Is.EqualTo(Tempo.Intense));
            Assert.That(back.Tactics.Value.Pressing, Is.EqualTo(Pressing.Press));
            Assert.That(back.Tactics.Value.Approach, Is.EqualTo(Approach.Possession));
            Assert.That(back.Eleven, Is.EqualTo(new List<int> { 0, 1, 2, -1 }));
            Assert.That(back.Market[0].Id.Value, Is.EqualTo(2));
            Assert.That(back.Market[0].Role, Is.EqualTo(PlayerRole.Striker));
            Assert.That(back.Morale[0].Player.Value, Is.EqualTo(1));
            Assert.That(back.Morale[0].Points, Is.EqualTo(-2.5).Within(1e-9));
            Assert.That(back.Morale[0].WeeksLeft, Is.EqualTo(3));
            Assert.That(back.Drama.Week, Is.EqualTo(9));
            Assert.That(back.Drama.LastFiredWeek, Is.EqualTo(7));
            Assert.That(back.Drama.FiredThisSeason, Is.EqualTo(2));
            Assert.That(back.Drama.Events[0].Event.Value, Is.EqualTo("fan-protest"));
            Assert.That(back.PendingEvent.Value, Is.EqualTo("budget-cut"));
            Assert.That(back.PendingSubjectPlayerId, Is.EqualTo(1));
        }

        [Test]
        public void Restore_ATacticalAxisThisBuildCannotRead_TakesTheNeutralValueInsteadOfFailing()
        {
            // The tolerant posture, on the one kind of field where it is right: a save outlives the build,
            // and an axis written by a build that knew a mentality this one does not should cost the manager
            // a dropdown, not the run. Contrast a PLAYER's role, three tests below, which fails the load —
            // there the default would be a lie the player can see (a squad quietly re-roled to goalkeeper).
            SeasonSaveData data = CreateV5Save();
            data.SchemaVersion = SeasonSaveData.CurrentVersion;
            data.Run = new RunSaveData
            {
                Tactics = new TacticsSaveData
                {
                    FormationName = "4-4-2",
                    Mentality = "Berserk",
                    Tempo = "Patient",
                    Pressing = null,
                    Approach = "Counter",
                },
            };

            Assert.That(new SaveMigrator().Migrate(data).IsSuccess, Is.True);
            RunState back = new SeasonSaveMapper().Restore(data).Run;

            Assert.That(back.Tactics.Value.Mentality, Is.EqualTo(Mentality.Balanced), "an unknown axis reads neutral");
            Assert.That(back.Tactics.Value.Pressing, Is.EqualTo(Pressing.Standard), "and so does a missing one");
            Assert.That(back.Tactics.Value.Tempo, Is.EqualTo(Tempo.Patient), "while the readable ones are kept");
            Assert.That(back.Tactics.Value.Approach, Is.EqualTo(Approach.Counter));
            Assert.That(back.Formation, Is.Null, "a shape with no slots is no shape — the run keeps its own");
        }

        [Test]
        public void Restore_AFormationWithAnUnreadableSlot_ComesBackAbsentRatherThanShort()
        {
            // All or nothing, and deliberately so: an eleven-slot shape quietly restored with ten would
            // bench somebody every week for the rest of the run, and nothing would ever say why.
            SeasonSaveData data = CreateV5Save();
            data.SchemaVersion = SeasonSaveData.CurrentVersion;
            data.Run = new RunSaveData
            {
                Tactics = new TacticsSaveData
                {
                    FormationName = "4-4-2",
                    FormationSlots = new List<string> { "Goalkeeper", "Libero", "CentreBack" },
                    Mentality = "Balanced",
                },
            };

            RunState back = new SeasonSaveMapper().Restore(data).Run;

            Assert.That(back.Formation, Is.Null);
            Assert.That(back.Tactics, Is.Not.Null, "the tactical axes are a separate fact and still read");
        }

        [Test]
        public void PersistedEnum_ReadsWhatItWrote_AndRejectsEverythingElse()
        {
            // The same three guards PersistedPlayerRole documents, now on the enums schema v6 added.
            Assert.That(PersistedEnum.ToName(Mentality.VeryAttacking), Is.EqualTo("VeryAttacking"));

            Assert.That(PersistedEnum.TryParse("VeryAttacking", out Mentality mentality), Is.True);
            Assert.That(mentality, Is.EqualTo(Mentality.VeryAttacking));

            Assert.That(PersistedEnum.TryParse("999", out Mentality _), Is.False, "a numeric string is not a member");
            Assert.That(PersistedEnum.TryParse("2", out Mentality _), Is.False, "not even one that IS a valid ordinal");
            Assert.That(PersistedEnum.TryParse("Balanced, Attacking", out Mentality _), Is.False, "nor a combination");
            Assert.That(PersistedEnum.TryParse("balanced", out Mentality _), Is.False, "the persisted name is matched exactly");
            Assert.That(PersistedEnum.TryParse(null, out Mentality _), Is.False);
            Assert.That(PersistedEnum.TryParse(string.Empty, out Mentality _), Is.False);

            Assert.That(PersistedEnum.ParseOr("Press", Pressing.Standard), Is.EqualTo(Pressing.Press));
            Assert.That(PersistedEnum.ParseOr("Stomp", Pressing.Standard), Is.EqualTo(Pressing.Standard));
            Assert.That(PersistedEnum.ParseOr(null, Approach.Counter), Is.EqualTo(Approach.Counter));
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
