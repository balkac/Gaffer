using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Locks the phase-4 exit criterion for traits: a trait must change the sim's output measurably
    /// (NON-NEGOTIABLE #7) — and only on its occasion, so a derby monster is a plain player in a plain
    /// fixture. The flavor trap (a trait that changes nothing) fails here.
    /// </summary>
    public sealed class TraitTests
    {
        private static Player PlayerAt(int id, Position position, byte stat, params string[] traits)
        {
            var attributes = new Attributes
            {
                Finishing = stat, Technique = stat, FirstTouch = stat, Dribbling = stat, Passing = stat,
                Crossing = stat, Heading = stat, LongShots = stat, Marking = stat, Tackling = stat,
                Penalties = stat, FreeKicks = stat, Corners = stat, LongThrows = stat,
                Pace = stat, Acceleration = stat, Stamina = stat, Strength = stat, Agility = stat,
                Jumping = stat, Balance = stat, Positioning = stat,
                Reflexes = stat, Handling = stat, AerialReach = stat, CommandOfArea = stat,
                OneOnOnes = stat, Kicking = stat, GkPositioning = stat,
            };

            var ids = new List<TraitId>(traits.Length);
            foreach (string trait in traits)
            {
                ids.Add(new TraitId(trait));
            }

            return new Player(new PlayerId(id), "Test Player", "England", position, 24, attributes, 70, ids);
        }

        private static IReadOnlyList<Player> UniformEleven(byte stat, params string[] strikerTraits)
        {
            return new List<Player>
            {
                PlayerAt(0, Position.Goalkeeper, stat),
                PlayerAt(1, Position.Defender, stat),
                PlayerAt(2, Position.Defender, stat),
                PlayerAt(3, Position.Defender, stat),
                PlayerAt(4, Position.Defender, stat),
                PlayerAt(5, Position.Midfielder, stat),
                PlayerAt(6, Position.Midfielder, stat),
                PlayerAt(7, Position.Midfielder, stat),
                PlayerAt(8, Position.Midfielder, stat),
                PlayerAt(9, Position.Forward, stat),
                PlayerAt(10, Position.Forward, stat, strikerTraits),
            };
        }

        private static MatchContext Plain()
        {
            return new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
        }

        private static MatchContext Derby()
        {
            return new MatchContext(MatchImportance.Derby, 10000, isTitleDecider: false, isRivalry: true);
        }

        private static MatchContext Final()
        {
            return new MatchContext(MatchImportance.Final, 10000, isTitleDecider: false, isRivalry: false);
        }

        [Test]
        public void Build_DerbyBeastInDerby_LiftsAttackMeasurably()
        {
            var builder = new EffectiveStrengthBuilder();
            IReadOnlyList<Player> eleven = UniformEleven(60, "derby-beast");

            TeamStrength plain = builder.Build(eleven, Tactics.Balanced, Plain());
            TeamStrength derby = builder.Build(eleven, Tactics.Balanced, Derby());

            // Two forwards, one a derby beast at x1.12: the attack axis rises by half of 12% of his rating.
            Assert.That(derby.Attack, Is.GreaterThan(plain.Attack));
            Assert.That(derby.Attack, Is.EqualTo(plain.Attack + (60.0 * 0.12 / 2.0)).Within(1e-9));
        }

        [Test]
        public void Build_DerbyBeastInPlainFixture_IsInert()
        {
            var builder = new EffectiveStrengthBuilder();
            TeamStrength withTrait = builder.Build(UniformEleven(60, "derby-beast"), Tactics.Balanced, Plain());
            TeamStrength without = builder.Build(UniformEleven(60), Tactics.Balanced, Plain());

            Assert.That(withTrait.Attack, Is.EqualTo(without.Attack).Within(1e-9));
            Assert.That(withTrait.Midfield, Is.EqualTo(without.Midfield).Within(1e-9));
            Assert.That(withTrait.Defence, Is.EqualTo(without.Defence).Within(1e-9));
        }

        [Test]
        public void Build_BigGameBottlerInFinal_LowersAttack()
        {
            var builder = new EffectiveStrengthBuilder();
            IReadOnlyList<Player> eleven = UniformEleven(60, "big-game-bottler");

            TeamStrength plain = builder.Build(eleven, Tactics.Balanced, Plain());
            TeamStrength final = builder.Build(eleven, Tactics.Balanced, Final());

            Assert.That(final.Attack, Is.LessThan(plain.Attack));
        }

        [Test]
        public void Build_ShowmanUnderBigCrowd_LiftsAttackOnlyPastThreshold()
        {
            var builder = new EffectiveStrengthBuilder();
            IReadOnlyList<Player> eleven = UniformEleven(60, "showman");

            TeamStrength smallCrowd = builder.Build(
                eleven, Tactics.Balanced, new MatchContext(MatchImportance.Normal, 8000, false, false));
            TeamStrength bigCrowd = builder.Build(
                eleven, Tactics.Balanced, new MatchContext(MatchImportance.Normal, 40000, false, false));
            TeamStrength baseline = builder.Build(UniformEleven(60), Tactics.Balanced, Plain());

            Assert.That(smallCrowd.Attack, Is.EqualTo(baseline.Attack).Within(1e-9));
            Assert.That(bigCrowd.Attack, Is.GreaterThan(baseline.Attack));
        }

        [Test]
        public void Build_DressingRoomLeader_LiftsTeammatesNotHimself()
        {
            var builder = new EffectiveStrengthBuilder();

            TeamStrength withLeader = builder.Build(UniformEleven(60, "dressing-room-leader"), Tactics.Balanced, Plain());
            TeamStrength without = builder.Build(UniformEleven(60), Tactics.Balanced, Plain());

            // The leader is the second forward. His aura reaches the other ten — defence and midfield rise —
            // and half the attack axis (the other forward) rises too, but his own rating stays put.
            Assert.That(withLeader.Defence, Is.GreaterThan(without.Defence));
            Assert.That(withLeader.Midfield, Is.GreaterThan(without.Midfield));
            double expectedAttack = (60.0 + (60.0 * 1.03)) / 2.0;
            Assert.That(withLeader.Attack, Is.EqualTo(expectedAttack).Within(1e-9));
        }

        [Test]
        public void Build_UnknownTraitId_IsIgnored()
        {
            var builder = new EffectiveStrengthBuilder();

            TeamStrength withUnknown = builder.Build(UniformEleven(60, "no-such-trait"), Tactics.Balanced, Derby());
            TeamStrength without = builder.Build(UniformEleven(60), Tactics.Balanced, Derby());

            Assert.That(withUnknown.Attack, Is.EqualTo(without.Attack).Within(1e-9));
        }

        [Test]
        public void Generate_SameSeed_ReproducesTraits()
        {
            var generator = new PlayerGenerator();
            var context = new GenerationContext();

            Player first = generator.Generate(new PlayerId(1), context, new SplitMix64RandomNumberGenerator(99UL));
            Player second = generator.Generate(new PlayerId(1), context, new SplitMix64RandomNumberGenerator(99UL));

            Assert.That(second.Traits, Is.EqualTo(first.Traits));
        }

        [Test]
        public void Generate_ManyPlayers_TraitShareStaysRare()
        {
            var generator = new PlayerGenerator();
            var context = new GenerationContext();
            var rng = new SplitMix64RandomNumberGenerator(7UL);

            int carriers = 0;
            const int total = 2000;
            for (int i = 0; i < total; i++)
            {
                if (generator.Generate(new PlayerId(i), context, rng).Traits.Count > 0)
                {
                    carriers++;
                }
            }

            // Traits are the personality layer, not a stat everyone has: around the authored 35%,
            // never a majority and never absent.
            Assert.That(carriers / (double)total, Is.InRange(0.25, 0.45));
        }

        [Test]
        public void Generate_ManyPlayers_EveryCatalogTraitAppears()
        {
            var generator = new PlayerGenerator();
            var context = new GenerationContext();
            var rng = new SplitMix64RandomNumberGenerator(11UL);

            var seen = new HashSet<TraitId>();
            for (int i = 0; i < 2000; i++)
            {
                foreach (TraitId id in generator.Generate(new PlayerId(i), context, rng).Traits)
                {
                    seen.Add(id);
                }
            }

            foreach (Trait trait in TraitCatalog.Default.Traits)
            {
                Assert.That(seen.Contains(trait.Id), Is.True, "trait never assigned: " + trait.Id.Value);
            }
        }

        [Test]
        public void Develop_PlayerWithTraits_CarriesThemAcrossSeasons()
        {
            var development = new Gaffer.Application.Progression.PlayerDevelopment();
            Player player = PlayerAt(5, Position.Forward, 60, "derby-beast", "glass-man");

            Player developed = development.Develop(player, new SplitMix64RandomNumberGenerator(3UL));

            Assert.That(developed.Traits, Is.EqualTo(player.Traits));
        }

        private static Player YoungProspect(params string[] traits)
        {
            var attributes = UniformAttributes(50);
            var ids = new List<TraitId>(traits.Length);
            foreach (string trait in traits)
            {
                ids.Add(new TraitId(trait));
            }

            return new Player(new PlayerId(40), "Prospect", "England", PlayerRole.Striker, 18, attributes, 92, ids);
        }

        private static Attributes UniformAttributes(byte stat)
        {
            return new Attributes
            {
                Finishing = stat, Technique = stat, FirstTouch = stat, Dribbling = stat, Passing = stat,
                Crossing = stat, Heading = stat, LongShots = stat, Marking = stat, Tackling = stat,
                Penalties = stat, FreeKicks = stat, Corners = stat, LongThrows = stat,
                Pace = stat, Acceleration = stat, Stamina = stat, Strength = stat, Agility = stat,
                Jumping = stat, Balance = stat, Positioning = stat,
                Reflexes = stat, Handling = stat, AerialReach = stat, CommandOfArea = stat,
                OneOnOnes = stat, Kicking = stat, GkPositioning = stat,
            };
        }

        [Test]
        public void Develop_TrainingDodger_GrowsLessThanIdenticalTwin()
        {
            var development = new Gaffer.Application.Progression.PlayerDevelopment();

            Player clean = development.Develop(YoungProspect(), new SplitMix64RandomNumberGenerator(5UL));
            Player dodger = development.Develop(YoungProspect("training-dodger"), new SplitMix64RandomNumberGenerator(5UL));

            // Same player, same seed, same season — the trait alone converts less of the gap.
            Assert.That(PlayerRatings.ForRole(dodger), Is.LessThan(PlayerRatings.ForRole(clean)));
        }

        [Test]
        public void Develop_ModelProfessional_OutgrowsIdenticalTwin()
        {
            var development = new Gaffer.Application.Progression.PlayerDevelopment();

            Player clean = development.Develop(YoungProspect(), new SplitMix64RandomNumberGenerator(5UL));
            Player professional = development.Develop(YoungProspect("model-professional"), new SplitMix64RandomNumberGenerator(5UL));

            Assert.That(PlayerRatings.ForRole(professional), Is.GreaterThan(PlayerRatings.ForRole(clean)));
        }

        [Test]
        public void Develop_GlassManAtThirty_DeclinesWhileIdenticalTwinHolds()
        {
            var development = new Gaffer.Application.Progression.PlayerDevelopment();
            // Same id, so both share the same per-player peak offset: the twin's onset is floored at 30+,
            // the glass man's sits three years earlier — at 30 only he has started to fade.
            var twin = new Player(new PlayerId(41), "Veteran", "England", PlayerRole.Striker, 30, UniformAttributes(70), 70);
            var glassMan = new Player(new PlayerId(41), "Veteran", "England", PlayerRole.Striker, 30, UniformAttributes(70), 70,
                new List<TraitId> { new TraitId("glass-man") });

            Player twinAfter = development.Develop(twin, new SplitMix64RandomNumberGenerator(9UL));
            Player glassAfter = development.Develop(glassMan, new SplitMix64RandomNumberGenerator(9UL));

            Assert.That(PlayerRatings.ForRole(twinAfter), Is.EqualTo(PlayerRatings.ForRole(twin)).Within(1e-9));
            Assert.That(PlayerRatings.ForRole(glassAfter), Is.LessThan(PlayerRatings.ForRole(glassMan)));
        }

        [Test]
        public void CaptureRestore_PlayerTraits_RoundTripBySlug()
        {
            var strengthBuilder = new EffectiveStrengthBuilder();
            var eleven = new List<Player>
            {
                PlayerAt(0, Position.Goalkeeper, 60),
                PlayerAt(1, Position.Defender, 60, "dressing-room-leader"),
                PlayerAt(2, Position.Forward, 60, "derby-beast", "glass-man"),
                PlayerAt(3, Position.Forward, 60, "from-a-future-catalog"),
            };
            var squad = new Squad(eleven);
            var league = new League("Save League", new List<Club>
            {
                new Club(new ClubId(0), "Traits FC", squad, strengthBuilder.Build(squad)),
                new Club(new ClubId(1), "Plain FC", new TeamStrength(55, 55, 55)),
            });

            var mapper = new Gaffer.Application.Serialization.SeasonSaveMapper();
            Gaffer.Application.Serialization.SeasonSaveData data = mapper.Capture(league, new LeagueSeason(league), 1UL, 1);
            Gaffer.Application.Serialization.RestoredSeason restored = mapper.Restore(data);

            IReadOnlyList<Player> players = restored.League.Clubs[0].Squad.Players;
            Assert.That(players[0].Traits, Is.Empty);
            Assert.That(players[1].Traits, Is.EqualTo(new[] { new TraitId("dressing-room-leader") }));
            Assert.That(players[2].Traits, Is.EqualTo(new[] { new TraitId("derby-beast"), new TraitId("glass-man") }));
            // An id the current catalog does not define is carried, not dropped — definitions rebind on use.
            Assert.That(players[3].Traits, Is.EqualTo(new[] { new TraitId("from-a-future-catalog") }));
        }

        [Test]
        public void Migrate_V3SaveWithoutTraits_RestoresTraitless()
        {
            var data = new Gaffer.Application.Serialization.SeasonSaveData
            {
                SchemaVersion = 3,
                LeagueName = "Old League",
                SeasonNumber = 2,
                MatchSeed = 5UL,
                PlayedRounds = 0,
            };
            data.Clubs.Add(new Gaffer.Application.Serialization.ClubSaveData
            {
                Id = 0,
                Name = "Old FC",
                Attack = 60, Midfield = 60, Defence = 60,
                Squad = new List<Gaffer.Application.Serialization.PlayerSaveData>
                {
                    new Gaffer.Application.Serialization.PlayerSaveData
                    {
                        Id = 1, Name = "Old Player", Nationality = "England", Role = (int)PlayerRole.Striker,
                        Age = 24, HiddenPotential = 70,
                        Attributes = new Gaffer.Application.Serialization.AttributesSaveData(),
                    },
                },
            });

            Result<Gaffer.Application.Serialization.SeasonSaveData> migrated = new Gaffer.Application.Serialization.SaveMigrator().Migrate(data);

            Assert.That(migrated.IsSuccess, Is.True);
            Assert.That(migrated.Value.SchemaVersion, Is.EqualTo(Gaffer.Application.Serialization.SaveSchema.CurrentVersion));
            Gaffer.Application.Serialization.RestoredSeason restored = new Gaffer.Application.Serialization.SeasonSaveMapper().Restore(migrated.Value);
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Traits, Is.Empty);
        }

        [Test]
        public void AdvanceWeek_DerbyBeastSquadUnderRivalryContext_ChangesTheMatchOutcome()
        {
            // End to end through the season loop: the same league, seed, and fixture — the only difference
            // is the stakes. A squad of derby beasts must play the derby measurably differently.
            LeagueSeason plainSeason = new LeagueSeason(TwoClubLeague());
            LeagueSeason derbySeason = new LeagueSeason(TwoClubLeague());
            MatchSimulator simulator = CreateSimulator();

            WeekResult plain = plainSeason.AdvanceWeek(simulator, Plain(), 42UL);
            WeekResult derby = derbySeason.AdvanceWeek(simulator, Derby(), 42UL);

            MatchResult plainMatch = plain.Matches[0];
            MatchResult derbyMatch = derby.Matches[0];
            bool sameScoreline = plainMatch.HomeGoals == derbyMatch.HomeGoals
                && plainMatch.AwayGoals == derbyMatch.AwayGoals
                && plainMatch.HomeShots == derbyMatch.HomeShots
                && plainMatch.AwayShots == derbyMatch.AwayShots;

            Assert.That(sameScoreline, Is.False);
        }

        private static League TwoClubLeague()
        {
            var strengthBuilder = new EffectiveStrengthBuilder();

            // Home: every outfielder a derby beast. Away: no traits at all.
            var beastEleven = new List<Player>();
            var plainEleven = new List<Player>();
            for (int i = 0; i < 11; i++)
            {
                Position position = i == 0 ? Position.Goalkeeper
                    : i <= 4 ? Position.Defender
                    : i <= 8 ? Position.Midfielder
                    : Position.Forward;
                beastEleven.Add(PlayerAt(i, position, 62, i == 0 ? new string[0] : new[] { "derby-beast" }));
                plainEleven.Add(PlayerAt(100 + i, position, 62));
            }

            var home = new Squad(beastEleven);
            var away = new Squad(plainEleven);
            return new League("Trait League", new List<Club>
            {
                new Club(new ClubId(0), "Beast FC", home, strengthBuilder.Build(home)),
                new Club(new ClubId(1), "Plain FC", away, strengthBuilder.Build(away)),
            });
        }

        private static MatchSimulator CreateSimulator()
        {
            return new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
        }
    }
}
