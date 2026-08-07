using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Pins the boundary guards on the numbers a designer can edit. Every divisor below comes straight
    /// from a ScriptableObject field, and float division does not throw — it poisons (CONVENTIONS §6):
    /// a zero turns into NaN or Infinity, every comparison against it is false, and the simulation goes
    /// on running with wrong answers and no error anywhere. These tests feed each guard the degenerate
    /// config and assert a sane, bounded result. The two fail-fast cases (§1/§4) are here too: a value
    /// the code has no answer for must raise, not quietly return a plausible-looking one.
    /// </summary>
    public sealed class BalanceGuardTests
    {
        private static Attributes Uniform(byte stat)
        {
            return new Attributes
            {
                Finishing = stat,
                Technique = stat,
                FirstTouch = stat,
                Dribbling = stat,
                Passing = stat,
                Crossing = stat,
                Heading = stat,
                LongShots = stat,
                Marking = stat,
                Tackling = stat,
                Penalties = stat,
                FreeKicks = stat,
                Corners = stat,
                LongThrows = stat,
                Pace = stat,
                Acceleration = stat,
                Stamina = stat,
                Strength = stat,
                Agility = stat,
                Jumping = stat,
                Balance = stat,
                Positioning = stat,
                Reflexes = stat,
                Handling = stat,
                AerialReach = stat,
                CommandOfArea = stat,
                OneOnOnes = stat,
                Kicking = stat,
                GkPositioning = stat,
            };
        }

        private static Player CreatePlayer(int id, PlayerRole role, int age = 24, byte stat = 60, string trait = null)
        {
            IReadOnlyList<TraitId> traits = trait == null
                ? null
                : new List<TraitId> { new TraitId(trait) };
            return new Player(new PlayerId(id), "P" + id, "England", role, age, Uniform(stat), 75, traits);
        }

        // --- EffectiveStrengthBuilder: the teammate-aura divisor ---------------------------------

        [Test]
        public void Build_TraitAuraOfZero_KeepsEveryAxisFiniteInsteadOfNaN()
        {
            // A trait asset saved with teammateAura 0 makes the lineup product 0, and the per-player
            // division that takes a leader back out of his own aura becomes 0/0. Untreated that is NaN
            // on all three axes, and NaN then makes every downstream `if (x < limit)` take the wrong
            // branch — the failure the guard exists to stop.
            var catalog = new TraitCatalog(new[]
            {
                new Trait(new TraitId("void-aura"), "trait.void_aura.name", 1.0, teammateAura: 0.0),
            });
            var builder = new EffectiveStrengthBuilder(catalog);
            var squad = new Squad(new List<Player>
            {
                CreatePlayer(0, PlayerRole.Goalkeeper, trait: "void-aura"),
                CreatePlayer(1, PlayerRole.CentreBack, trait: "void-aura"),
                CreatePlayer(2, PlayerRole.CentralMidfield, trait: "void-aura"),
                CreatePlayer(3, PlayerRole.Striker, trait: "void-aura"),
            });

            TeamStrength strength = builder.Build(squad);

            Assert.That(double.IsNaN(strength.Attack), Is.False, "a zeroed aura must not poison the attack axis");
            Assert.That(double.IsNaN(strength.Midfield), Is.False);
            Assert.That(double.IsNaN(strength.Defence), Is.False);
            Assert.That(double.IsInfinity(strength.Attack), Is.False);
            Assert.That(strength.Attack, Is.GreaterThan(0.0));
        }

        [Test]
        public void Build_CalibratedTraitCatalog_IsUnchangedByTheAuraGuard()
        {
            // The guard's band sits far outside every authored aura, so the shipped catalog must produce
            // exactly the strength a neutral, trait-free lineup does at the same ratings.
            var withLeader = new Squad(new List<Player>
            {
                CreatePlayer(0, PlayerRole.CentreBack),
                CreatePlayer(1, PlayerRole.CentralMidfield),
                CreatePlayer(2, PlayerRole.Striker, trait: "dressing-room-leader"),
            });
            var withoutLeader = new Squad(new List<Player>
            {
                CreatePlayer(0, PlayerRole.CentreBack),
                CreatePlayer(1, PlayerRole.CentralMidfield),
                CreatePlayer(2, PlayerRole.Striker),
            });
            var builder = new EffectiveStrengthBuilder(TraitCatalog.Default);

            TeamStrength led = builder.Build(withLeader);
            TeamStrength plain = builder.Build(withoutLeader);

            // The leader lifts the other two by 1.03 and himself by nothing — the calibrated behaviour,
            // untouched by the clamp.
            Assert.That(led.Attack, Is.EqualTo(plain.Attack).Within(1e-9), "the leader does not lift himself");
            Assert.That(led.Defence, Is.EqualTo(plain.Defence * 1.03).Within(1e-9));
        }

        [Test]
        public void Build_EveryPlayerRole_ContributesToAStrengthAxis()
        {
            // The switch on Position now throws on an unhandled value rather than silently diluting all
            // three axes. Position is derived from PlayerRole, so this is the reachable form of that
            // guard: a role added tomorrow whose line has no axis fails here, by name.
            var builder = new EffectiveStrengthBuilder();
            foreach (PlayerRole role in Enum.GetValues(typeof(PlayerRole)))
            {
                var squad = new Squad(new List<Player> { CreatePlayer(0, role) });

                TeamStrength strength = builder.Build(squad);

                Assert.That(strength.Attack, Is.GreaterThan(0.0), "role " + role + " contributes to no axis");
                Assert.That(strength.Midfield, Is.GreaterThan(0.0), "role " + role + " contributes to no axis");
                Assert.That(strength.Defence, Is.GreaterThan(0.0), "role " + role + " contributes to no axis");
            }
        }

        // --- PoissonChanceGenerator: the strength-ratio cap and the sampling lambda ---------------

        private static MatchCommand EvenMatch()
        {
            return new MatchCommand(new TeamStrength(70.0, 70.0, 70.0), new TeamStrength(70.0, 70.0, 70.0), default);
        }

        [Test]
        public void GenerateChances_ZeroMaxStrengthRatio_ProducesAFootballNumberOfChances()
        {
            // A zeroed cap makes the ratio floor 1/0 = Infinity, which drives the expected count to
            // Infinity and the Knuth loop to hundreds of iterations — roughly 750 bogus chances in one
            // match, with nothing thrown. Held at 1, both sides get an ordinary allocation.
            var settings = new MatchSimulationSettings(7.0, 0.17, 1.15, 0.0);
            var generator = new PoissonChanceGenerator(settings);

            IReadOnlyList<Chance> chances = generator.GenerateChances(EvenMatch(), new SplitMix64RandomNumberGenerator(7UL));

            Assert.That(chances.Count, Is.LessThan(40), "a zeroed cap must not flood the match with chances");
            foreach (Chance chance in chances)
            {
                Assert.That(double.IsNaN(chance.Quality), Is.False);
                Assert.That(chance.Minute, Is.InRange(1, 90));
            }
        }

        [Test]
        public void GenerateChances_RunawayChanceVolume_IsCappedInsteadOfLooping()
        {
            // The Knuth sampler runs on the order of lambda iterations, so an absurd base volume is not
            // an exception — it is an unbounded loop building an unbounded list. The cap turns that into
            // a bounded, if silly, match.
            var settings = new MatchSimulationSettings(10_000.0, 0.17, 1.15, 2.0);
            var generator = new PoissonChanceGenerator(settings);

            IReadOnlyList<Chance> chances = generator.GenerateChances(EvenMatch(), new SplitMix64RandomNumberGenerator(11UL));

            Assert.That(chances.Count, Is.GreaterThan(0));
            Assert.That(chances.Count, Is.LessThan(400), "the expected count is capped well below a runaway loop");
        }

        [Test]
        public void GenerateChances_CalibratedSettings_AreUnchangedByTheGuards()
        {
            // Every guard above must be a no-op at the shipped calibration: same seed, same chances.
            var generator = new PoissonChanceGenerator(MatchSimulationSettings.Default);
            var mismatch = new MatchCommand(new TeamStrength(80.0, 75.0, 70.0), new TeamStrength(55.0, 60.0, 58.0), default);

            IReadOnlyList<Chance> first = generator.GenerateChances(mismatch, new SplitMix64RandomNumberGenerator(3UL));
            var snapshot = new List<Chance>(first);
            IReadOnlyList<Chance> second = generator.GenerateChances(mismatch, new SplitMix64RandomNumberGenerator(3UL));

            Assert.That(second.Count, Is.EqualTo(snapshot.Count));
            Assert.That(snapshot.Count, Is.InRange(2, 60), "a calibrated match still makes a football number of chances");
            for (int i = 0; i < snapshot.Count; i++)
            {
                Assert.That(second[i].Minute, Is.EqualTo(snapshot[i].Minute));
                Assert.That(second[i].Quality, Is.EqualTo(snapshot[i].Quality).Within(1e-12));
            }
        }

        // --- SquadRenewal: the twilight-band divisor ----------------------------------------------

        [Test]
        public void Renew_HardAgeNotPastTwilight_StillRetiresVeterans()
        {
            // Twilight and Hard on the same year makes the retirement odds a 0/0 — NaN, and
            // `rng.NextDouble() < NaN` is false, so nobody would ever retire and squads would age
            // forever. The band is clamped, so a veteran at the threshold still goes.
            var settings = new RenewalSettings(outfielderTwilightAge: 34, outfielderHardAge: 34);
            var squad = new Squad(new List<Player>
            {
                CreatePlayer(0, PlayerRole.CentreBack, age: 36),
                CreatePlayer(1, PlayerRole.Striker, age: 23),
            });
            int nextId = 1000;

            Squad renewed = new SquadRenewal(new PlayerGenerator(), settings).Renew(squad, 99UL, 2, ref nextId);

            Assert.That(HasId(renewed, 0), Is.False, "a degenerate retirement band must not freeze retirement");
            Assert.That(HasId(renewed, 1), Is.True);
        }

        // --- SquadRenewal: the reusable thinnest-role buffers --------------------------------------

        [Test]
        public void Renew_AcademyIntake_SpreadsAcrossTheThinnestRoles()
        {
            // The role search now counts into a reusable int[] indexed by (int)PlayerRole instead of
            // allocating a dictionary and a list per youth. The behaviour it must preserve: successive
            // arrivals go to distinct roles the squad has none of.
            var settings = new RenewalSettings(youthIntakePerSeason: 5);
            var squad = new Squad(new List<Player>
            {
                CreatePlayer(0, PlayerRole.Striker, age: 22),
                CreatePlayer(1, PlayerRole.Striker, age: 23),
                CreatePlayer(2, PlayerRole.Striker, age: 24),
            });
            int nextId = 1000;

            Squad renewed = new SquadRenewal(new PlayerGenerator(), settings).Renew(squad, 99UL, 2, ref nextId);

            var intakeRoles = new List<PlayerRole>();
            foreach (Player player in renewed.Players)
            {
                if (player.Id.Value >= 1000)
                {
                    intakeRoles.Add(player.Role);
                }
            }

            Assert.That(intakeRoles.Count, Is.EqualTo(5));
            var seen = new HashSet<PlayerRole>();
            foreach (PlayerRole role in intakeRoles)
            {
                Assert.That(role, Is.Not.EqualTo(PlayerRole.Striker), "the one role the squad is deep in is never the thinnest");
                Assert.That(seen.Add(role), Is.True, "each arrival fills a different position of need");
            }
        }

        [Test]
        public void Renew_ReusedRenewalInstance_MatchesAFreshOne()
        {
            // The role buffers are fields cleared per call, so the second use must see a clean state —
            // the buffer-reuse contract CONVENTIONS §5 asks a pooled scratch to prove.
            var settings = new RenewalSettings(youthIntakePerSeason: 3);
            var squad = new Squad(new List<Player>
            {
                CreatePlayer(0, PlayerRole.Goalkeeper, age: 25),
                CreatePlayer(1, PlayerRole.CentreBack, age: 24),
                CreatePlayer(2, PlayerRole.Striker, age: 26),
            });

            var reused = new SquadRenewal(new PlayerGenerator(), settings);
            int warmupId = 500;
            reused.Renew(squad, 12UL, 1, ref warmupId);

            int reusedId = 1000;
            Squad fromReused = reused.Renew(squad, 99UL, 2, ref reusedId);
            int freshId = 1000;
            Squad fromFresh = new SquadRenewal(new PlayerGenerator(), settings).Renew(squad, 99UL, 2, ref freshId);

            Assert.That(reusedId, Is.EqualTo(freshId));
            Assert.That(fromReused.Players.Count, Is.EqualTo(fromFresh.Players.Count));
            for (int i = 0; i < fromReused.Players.Count; i++)
            {
                Assert.That(fromReused.Players[i].Id.Value, Is.EqualTo(fromFresh.Players[i].Id.Value));
                Assert.That(fromReused.Players[i].Role, Is.EqualTo(fromFresh.Players[i].Role));
                Assert.That(fromReused.Players[i].Attributes, Is.EqualTo(fromFresh.Players[i].Attributes));
            }
        }

        // --- Economy: the two rounding-step divisors -----------------------------------------------

        [Test]
        public void Value_ZeroValuationRounding_StillPricesThePlayer()
        {
            // raw/0 is Infinity, the cast to long collapses it, and every player in the game becomes
            // free — a silent economy wipe. A step below one currency unit means nothing, so 1 is the
            // floor the guard holds.
            Player player = CreatePlayer(0, PlayerRole.Striker, age: 25, stat: 80);
            var broken = new EconomySettings(valuationRounding: 0);
            var unrounded = new EconomySettings(valuationRounding: 1);

            long value = PlayerValuation.Value(player, broken);

            Assert.That(value, Is.GreaterThan(0L), "a zeroed rounding step must not make transfers free");
            Assert.That(value, Is.EqualTo(PlayerValuation.Value(player, unrounded)));
        }

        [Test]
        public void Weekly_ZeroWageRounding_StillPricesThePlayer()
        {
            Player player = CreatePlayer(0, PlayerRole.Striker, age: 25, stat: 80);
            var broken = new EconomySettings(wageRounding: 0);
            var unrounded = new EconomySettings(wageRounding: 1);

            long wage = PlayerWage.Weekly(player, broken);

            Assert.That(wage, Is.GreaterThan(EconomySettings.Default.WageFloor), "a zeroed rounding step must not flatten wages to the floor");
            Assert.That(wage, Is.EqualTo(PlayerWage.Weekly(player, unrounded)));
        }

        [Test]
        public void Value_CalibratedEconomy_IsUnchangedByTheRoundingGuard()
        {
            Player player = CreatePlayer(0, PlayerRole.Striker, age: 25, stat: 80);

            Assert.That(PlayerValuation.Value(player), Is.EqualTo(PlayerValuation.Value(player, EconomySettings.Default)));
            Assert.That(PlayerValuation.Value(player) % EconomySettings.Default.ValuationRounding, Is.EqualTo(0L));
            Assert.That(PlayerWage.Weekly(player) % EconomySettings.Default.WageRounding, Is.EqualTo(0L));
        }

        // --- The two fail-fast guards ---------------------------------------------------------------

        [Test]
        public void Resolve_UnhandledEffectKind_ThrowsInsteadOfDoingNothing()
        {
            // "Every kind changes real state" is the rule DramaEffect states. A kind with no arm in the
            // resolver would be a silent no-op — a decided, consequential event that consequences
            // nothing. That is a broken invariant, not an expected failure, so it raises (§1/§4).
            var effects = new[] { new DramaEffect((DramaEffectKind)99, 1.0, 2) };
            var dramaEvent = new DramaEvent(
                new DramaEventId("unknown-effect"), DramaCategory.Personal, "drama.test.title", "drama.test.body",
                false, new DramaTrigger(), 1.0, 0,
                new[] { new DramaChoice("drama.test.yes", effects) });
            var squad = new List<Player> { CreatePlayer(0, PlayerRole.Striker) };
            var pending = new PendingDrama(dramaEvent, null, new DramaWeekContext(squad, null, 10, 0, false));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new DramaEngine().Resolve(pending, 0));
        }

        [Test]
        public void Evaluate_ManagedClubNotInTheTable_ThrowsInsteadOfSacking()
        {
            // The league position of a club that is not in the league has no sensible answer. The old
            // fallback returned last place, which read as a real relegation and ended the run.
            var table = new LeagueTable(new List<ClubId> { new ClubId(0), new ClubId(1), new ClubId(2) });

            Assert.Throws<ArgumentException>(
                () => new SeasonEvaluator().Evaluate(table, new ClubId(42), new BoardTarget(1, 2)));
        }

        private static bool HasId(Squad squad, int id)
        {
            foreach (Player player in squad.Players)
            {
                if (player.Id.Value == id)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
