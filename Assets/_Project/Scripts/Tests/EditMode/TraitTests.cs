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
