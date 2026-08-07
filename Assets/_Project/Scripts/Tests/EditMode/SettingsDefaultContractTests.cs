using System;
using System.Collections.Generic;
using System.Reflection;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Pins the one <c>Default</c> convention every balance/settings object in the core follows —
    /// written down here because this is the file that enforces it, and because the codebase used to
    /// hold two opposite conventions behind an identical-looking API: some <c>Default</c>s handed back
    /// a cached shared instance, others a fresh one per read. <c>DramaSettings.Default.MaxEventsPerSeason = 6</c>
    /// was a silent no-op; the same line on <c>EconomySettings</c> repriced every transfer in the
    /// process. NUnit runs the whole suite in one process, so that was a live test-pollution vector too.
    ///
    /// <para><b>The convention</b>, for every settings type under <c>Gaffer.Application</c>:</para>
    /// <list type="number">
    /// <item>Every property is <c>{ get; init; }</c> (C# 9). The object is immutable once built;
    /// object-initializer ergonomics at construction sites are unchanged, which is why <c>init</c> and
    /// not a constructor.</item>
    /// <item><c>Default</c> is <c>public static X Default { get; } = new X();</c> — an auto-property
    /// initializer, evaluated once and cached. Never <c>=> new X()</c>: an expression-bodied property
    /// is a method body and re-allocates on every single access (PERFORMANCE.md §8, "Shared preset
    /// data lives in static readonly fields, never expression-bodied properties" — and the confusable
    /// that IS cached is exactly the <c>{ get; } =</c> form).</item>
    /// <item>The one exception: a settings type that is a <c>struct</c>
    /// (<see cref="MatchSimulationSettings"/>) keeps <c>=></c>, because <c>new</c> on a struct
    /// allocates nothing (PERFORMANCE.md §8, "Free: <c>new</c> structs") and each read hands back an
    /// independent copy that cannot be aliased.</item>
    /// </list>
    ///
    /// <para>(1) is what makes (2) safe, and they are one change, not two: caching an instance whose
    /// properties still had public setters would hand every caller the same mutable object — a
    /// process-wide shared-state bug instead of a wasted allocation.</para>
    ///
    /// <para>The tests below discover the settings types by reflection rather than a hand-kept list, so
    /// a new one is held to the convention the day it is written.</para>
    /// </summary>
    public sealed class SettingsDefaultContractTests
    {
        // Every public type in the Application assembly that publishes `public static X Default`. The
        // namespace filter matters: the headless bridge compiles Common, Domain, Application and the
        // tests into one assembly, and Domain's TraitCatalog/DramaCatalog are content catalogs, not
        // balance objects, so scanning the raw assembly would mean this test covers a different set
        // under `dotnet test` than under the Unity Test Runner.
        private static List<Type> SettingsTypes()
        {
            var found = new List<Type>();
            foreach (Type type in typeof(RenewalSettings).Assembly.GetTypes())
            {
                if (!type.IsPublic || type.Namespace == null || !type.Namespace.StartsWith("Gaffer.Application", StringComparison.Ordinal))
                {
                    continue;
                }

                PropertyInfo defaults = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (defaults != null && defaults.PropertyType == type)
                {
                    found.Add(type);
                }
            }

            return found;
        }

        [Test]
        public void SettingsTypes_AreDiscovered()
        {
            // A guard on the guard: if the reflection scan ever silently matched nothing, every test
            // below would pass vacuously. Named rather than counted, so adding a settings type does not
            // fail an unrelated assertion — it just gets held to the convention.
            List<Type> found = SettingsTypes();
            string names = string.Join(", ", found.ConvertAll(t => t.Name));

            Assert.That(found, Contains.Item(typeof(MatchSimulationSettings)), names);
            Assert.That(found, Contains.Item(typeof(TacticsSettings)), names);
            Assert.That(found, Contains.Item(typeof(ScorerWeights)), names);
            Assert.That(found, Contains.Item(typeof(RenewalSettings)), names);
            Assert.That(found, Contains.Item(typeof(Gaffer.Application.Progression.DevelopmentSettings)), names);
            Assert.That(found, Contains.Item(typeof(Gaffer.Application.Drama.DramaSettings)), names);
            Assert.That(found, Contains.Item(typeof(Gaffer.Application.Drama.MoraleSettings)), names);
            Assert.That(found, Contains.Item(typeof(Gaffer.Application.Transfers.EconomySettings)), names);
        }

        [Test]
        public void Default_ReadTwice_IsTheSameCachedInstance()
        {
            foreach (Type type in SettingsTypes())
            {
                if (type.IsValueType)
                {
                    // A struct Default is a copy per read by definition; caching is meaningless and
                    // costs nothing to skip. Its own case is covered below.
                    continue;
                }

                PropertyInfo defaults = type.GetProperty("Default", BindingFlags.Public | BindingFlags.Static);
                object first = defaults.GetValue(null);
                object second = defaults.GetValue(null);

                Assert.That(first, Is.Not.Null, type.Name);
                Assert.That(second, Is.SameAs(first),
                    type.Name + ".Default must be `{ get; } = new " + type.Name + "()`, not `=> new " + type.Name +
                    "()` — the expression-bodied form is a method body and allocates on every access (PERFORMANCE §8).");
            }
        }

        [Test]
        public void SettingsTypes_HaveNoPropertyThatCanBeSetAfterConstruction()
        {
            foreach (Type type in SettingsTypes())
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    MethodInfo setter = property.SetMethod;
                    if (setter == null || !setter.IsPublic)
                    {
                        continue;
                    }

                    // An `init` accessor is an ordinary setter carrying a required custom modifier of
                    // System.Runtime.CompilerServices.IsExternalInit on its signature — that modreq is
                    // the whole difference, and it is what stops a caller writing to a shared Default.
                    bool initOnly = false;
                    foreach (Type modifier in setter.ReturnParameter.GetRequiredCustomModifiers())
                    {
                        if (modifier.Name == "IsExternalInit")
                        {
                            initOnly = true;
                        }
                    }

                    Assert.That(initOnly, Is.True,
                        type.Name + "." + property.Name + " has a public setter. Default is a cached shared " +
                        "instance, so a settable property is process-wide mutable state — make it `{ get; init; }`.");
                }
            }
        }

        [Test]
        public void MatchSimulationSettings_IsAStructSoItsDefaultMayStayExpressionBodied()
        {
            // The exception is load-bearing, so it is asserted rather than left as a comment: if this
            // type is ever turned into a class, the `=> new(...)` on its Default silently becomes an
            // allocation on every read and this test is the thing that says so.
            Assert.That(typeof(MatchSimulationSettings).IsValueType, Is.True);
            Assert.That(MatchSimulationSettings.Default.BaseChancesPerTeam, Is.EqualTo(7.0));
            Assert.That(MatchSimulationSettings.Default.MeanChanceQuality, Is.EqualTo(0.17));
            Assert.That(MatchSimulationSettings.Default.HomeAdvantage, Is.EqualTo(1.15));
            Assert.That(MatchSimulationSettings.Default.MaxStrengthRatio, Is.EqualTo(2.0));
        }

        // --- The youth band that moved out of SquadRenewal into RenewalSettings -------------------

        // Exactly the literals SquadRenewal.YouthContext/AverageRating held before the band became
        // config (NON-NEGOTIABLE #3): `average-25` clamped to [25,60], `average-8` to [35,72],
        // `average-3` to [45,85], `average+18` to [60,95], and 50 as the empty-squad average. Spelled
        // out here, not read from RenewalSettings, so this is a real comparison and not a tautology.
        private static RenewalSettings TheOldLiterals()
        {
            return new RenewalSettings
            {
                YouthMinAbilityOffset = -25,
                YouthMinAbilityFloor = 25,
                YouthMinAbilityCeiling = 60,
                YouthMaxAbilityOffset = -8,
                YouthMaxAbilityFloor = 35,
                YouthMaxAbilityCeiling = 72,
                YouthMinPotentialOffset = -3,
                YouthMinPotentialFloor = 45,
                YouthMinPotentialCeiling = 85,
                YouthMaxPotentialOffset = 18,
                YouthMaxPotentialFloor = 60,
                YouthMaxPotentialCeiling = 95,
                EmptySquadAverageRating = 50,
            };
        }

        [Test]
        public void RenewalSettingsDefault_YouthBand_StillHoldsTheLiteralsItReplaced()
        {
            RenewalSettings shipped = RenewalSettings.Default;
            RenewalSettings old = TheOldLiterals();

            Assert.That(shipped.YouthMinAbilityOffset, Is.EqualTo(old.YouthMinAbilityOffset));
            Assert.That(shipped.YouthMinAbilityFloor, Is.EqualTo(old.YouthMinAbilityFloor));
            Assert.That(shipped.YouthMinAbilityCeiling, Is.EqualTo(old.YouthMinAbilityCeiling));
            Assert.That(shipped.YouthMaxAbilityOffset, Is.EqualTo(old.YouthMaxAbilityOffset));
            Assert.That(shipped.YouthMaxAbilityFloor, Is.EqualTo(old.YouthMaxAbilityFloor));
            Assert.That(shipped.YouthMaxAbilityCeiling, Is.EqualTo(old.YouthMaxAbilityCeiling));
            Assert.That(shipped.YouthMinPotentialOffset, Is.EqualTo(old.YouthMinPotentialOffset));
            Assert.That(shipped.YouthMinPotentialFloor, Is.EqualTo(old.YouthMinPotentialFloor));
            Assert.That(shipped.YouthMinPotentialCeiling, Is.EqualTo(old.YouthMinPotentialCeiling));
            Assert.That(shipped.YouthMaxPotentialOffset, Is.EqualTo(old.YouthMaxPotentialOffset));
            Assert.That(shipped.YouthMaxPotentialFloor, Is.EqualTo(old.YouthMaxPotentialFloor));
            Assert.That(shipped.YouthMaxPotentialCeiling, Is.EqualTo(old.YouthMaxPotentialCeiling));
            Assert.That(shipped.EmptySquadAverageRating, Is.EqualTo(old.EmptySquadAverageRating));
        }

        [Test]
        public void Renew_WithTheDefaultBand_ProducesTheSameSquadAsTheOldLiterals()
        {
            // Mid-table squad: the average lands inside every band, so the offsets — not the clamps —
            // decide the intake, and a wrong offset would show.
            Squad squad = SquadOf(
                P(0, PlayerRole.Goalkeeper, 27, 62), P(1, PlayerRole.CentreBack, 30, 58),
                P(2, PlayerRole.CentralMidfield, 24, 66), P(3, PlayerRole.Striker, 39, 55));

            AssertRenewsIdentically(squad, "a mid-table squad");
        }

        [Test]
        public void Renew_AtTheBandEdges_ProducesTheSameSquadAsTheOldLiterals()
        {
            // A squad far outside the band: every edge saturates against its floor or ceiling, so this
            // is the case a wrong floor/ceiling breaks and the offsets alone cannot cover.
            Squad weak = SquadOf(P(0, PlayerRole.Goalkeeper, 25, 20), P(1, PlayerRole.Striker, 24, 22));
            Squad strong = SquadOf(P(0, PlayerRole.Goalkeeper, 25, 95), P(1, PlayerRole.Striker, 24, 97));

            AssertRenewsIdentically(weak, "a squad below every floor");
            AssertRenewsIdentically(strong, "a squad above every ceiling");
        }

        [Test]
        public void Renew_EmptySquad_ProducesTheSameYouthAsTheOldFallbackAverage()
        {
            // The `50` fallback moved out of AverageRating with the rest of the band; with no players to
            // average it is the only thing deciding what the academy produces.
            AssertRenewsIdentically(new Squad(new List<Player>()), "an empty squad");
        }

        [Test]
        public void Renew_YouthBand_StillTracksTheSquadsOwnLevel()
        {
            // Tier persistence, the reason the band is offsets-from-average at all: prove the settings
            // are actually read per squad rather than collapsing to one constant band.
            Squad weak = SquadOf(P(0, PlayerRole.Striker, 24, 40), P(1, PlayerRole.CentreBack, 25, 42));
            Squad strong = SquadOf(P(0, PlayerRole.Striker, 24, 80), P(1, PlayerRole.CentreBack, 25, 82));

            int weakId = 1000;
            int strongId = 1000;
            Player weakYouth = OnlyFresh(new SquadRenewal(new PlayerGenerator()).Renew(weak, 99UL, 2, ref weakId));
            Player strongYouth = OnlyFresh(new SquadRenewal(new PlayerGenerator()).Renew(strong, 99UL, 2, ref strongId));

            Assert.That(strongYouth.HiddenPotential, Is.GreaterThan(weakYouth.HiddenPotential),
                "a stronger club's academy must produce a higher ceiling — the band is drawn around the squad's own level");
        }

        private static void AssertRenewsIdentically(Squad squad, string what)
        {
            int shippedId = 1000;
            int oldId = 1000;
            Squad shipped = new SquadRenewal(new PlayerGenerator()).Renew(squad, 4242UL, 3, ref shippedId, seedGem: false);
            Squad old = new SquadRenewal(new PlayerGenerator(), TheOldLiterals()).Renew(squad, 4242UL, 3, ref oldId, seedGem: false);

            Assert.That(shippedId, Is.EqualTo(oldId), what + ": the same number of ids handed out");
            Assert.That(shipped.Players.Count, Is.EqualTo(old.Players.Count), what);

            for (int i = 0; i < shipped.Players.Count; i++)
            {
                Player a = shipped.Players[i];
                Player b = old.Players[i];
                string where = what + ", player " + i;

                Assert.That(a.Id.Value, Is.EqualTo(b.Id.Value), where);
                Assert.That(a.Name, Is.EqualTo(b.Name), where);
                Assert.That(a.Nationality, Is.EqualTo(b.Nationality), where);
                Assert.That(a.Role, Is.EqualTo(b.Role), where);
                Assert.That(a.Age, Is.EqualTo(b.Age), where);
                Assert.That(a.HiddenPotential, Is.EqualTo(b.HiddenPotential), where);
                Assert.That(a.Attributes, Is.EqualTo(b.Attributes), where);
                Assert.That(a.Traits.Count, Is.EqualTo(b.Traits.Count), where);
                for (int t = 0; t < a.Traits.Count; t++)
                {
                    Assert.That(a.Traits[t].Value, Is.EqualTo(b.Traits[t].Value), where);
                }
            }
        }

        private static Player OnlyFresh(Squad squad)
        {
            foreach (Player player in squad.Players)
            {
                if (player.Id.Value >= 1000)
                {
                    return player;
                }
            }

            Assert.Fail("no academy youth joined");
            return null;
        }

        private static Squad SquadOf(params Player[] players)
        {
            return new Squad(new List<Player>(players));
        }

        private static Player P(int id, PlayerRole role, int age, byte stat)
        {
            return new Player(new PlayerId(id), "P" + id, "England", role, age, Uniform(stat), 75);
        }

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
    }
}
