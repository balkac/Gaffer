using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Traits;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Locks the generation/season agreement that the hardwired strength builder used to break. A club's
    /// <see cref="Club.Strength"/> is written once at world generation and PERSISTED — it is the only
    /// strength a restored, squad-less club ever has — while <see cref="LeagueSeason"/> re-derives strength
    /// through the trait catalog it was CONFIGURED with. When the generator binds
    /// <see cref="TraitCatalog.Default"/> in a field initializer, an authored catalog that retunes a
    /// teammate aura produces a league generated under one set of rules and played under another, and the
    /// difference is invisible: both numbers look perfectly plausible. So the catalog is injected
    /// (ARCHITECTURE §6 — wiring belongs to the composition root, not a collaborator's field), and these
    /// tests fail if it is ever bound internally again.
    /// </summary>
    public sealed class LeagueGeneratorCatalogTests
    {
        private const int ClubCount = 4;

        /// <summary>A catalog that is NOT the default and that the strength builder can feel: a single
        /// common trait with a large teammate aura, so a squad carrying it derives measurably differently.
        /// Deliberately a slug the built-in catalog does not define, so a generator still wired to the
        /// default would resolve it to null and produce the unmodified strength.</summary>
        private static TraitCatalog AuraCatalog()
        {
            return new TraitCatalog(new[]
            {
                new Trait(new TraitId("house-aura"), "trait.house_aura.name", 1.0, teammateAura: 1.4),
            });
        }

        [Test]
        public void Generate_WithANonDefaultCatalog_WritesStrengthsDerivedThroughThatCatalog()
        {
            TraitCatalog catalog = AuraCatalog();
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator(catalog)), catalog);

            League league = generator.Generate(ClubCount, new SplitMix64RandomNumberGenerator(4242UL));

            // The season's rule for the same clubs: the SAME catalog, through the same builder type.
            var seasonRules = new EffectiveStrengthBuilder(catalog);
            foreach (Club club in league.Clubs)
            {
                TeamStrength expected = seasonRules.Build(club.Squad);
                AssertSameStrength(club.Strength, expected, club.Name);
            }
        }

        [Test]
        public void Generate_WithANonDefaultCatalog_DiffersFromTheDefaultCatalogDerivation()
        {
            // Guards the guard: without this, the test above would still pass if the catalog were ignored
            // and both sides silently fell back to the built-in set.
            TraitCatalog catalog = AuraCatalog();
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator(catalog)), catalog);

            League league = generator.Generate(ClubCount, new SplitMix64RandomNumberGenerator(4242UL));

            var defaultRules = new EffectiveStrengthBuilder(TraitCatalog.Default);
            bool anyClubDiffers = false;
            foreach (Club club in league.Clubs)
            {
                TeamStrength underDefault = defaultRules.Build(club.Squad);
                if (!Same(club.Strength.Attack, underDefault.Attack)
                    || !Same(club.Strength.Midfield, underDefault.Midfield)
                    || !Same(club.Strength.Defence, underDefault.Defence))
                {
                    anyClubDiffers = true;
                }
            }

            Assert.That(anyClubDiffers, Is.True, "The authored catalog must actually move the derived strength.");
        }

        [Test]
        public void Generate_WithAnInjectedBuilder_UsesThatBuilderRatherThanBuildingItsOwn()
        {
            // The fully-injected form the composition root uses: the very builder the rest of the graph
            // shares, tactics balance included, so no collaborator can construct a second one on other
            // balance.
            TraitCatalog catalog = AuraCatalog();
            var shared = new EffectiveStrengthBuilder(catalog, TacticsSettings.Default);
            var generator = new LeagueGenerator(
                new SquadGenerator(new PlayerGenerator(catalog)), shared, new ClubNameGenerator());

            League league = generator.Generate(ClubCount, new SplitMix64RandomNumberGenerator(99UL));

            foreach (Club club in league.Clubs)
            {
                AssertSameStrength(club.Strength, shared.Build(club.Squad), club.Name);
            }
        }

        [Test]
        public void Generate_WithTheDefaultingConstructor_StillDerivesThroughTheBuiltInCatalog()
        {
            // The compatibility path (editor windows, headless harnesses) keeps its old meaning: generation
            // on the built-in catalog, which is correct exactly when the season also plays on it (§7).
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator()));

            League league = generator.Generate(ClubCount, new SplitMix64RandomNumberGenerator(7UL));

            var builtIn = new EffectiveStrengthBuilder(TraitCatalog.Default);
            foreach (Club club in league.Clubs)
            {
                AssertSameStrength(club.Strength, builtIn.Build(club.Squad), club.Name);
            }
        }

        [Test]
        public void Generate_SameSeedAndCatalog_ReproducesTheSameLeague()
        {
            // Injection must not have touched the rng draw order — determinism is the project's
            // non-negotiable, so the same seed still produces the same world.
            TraitCatalog catalog = AuraCatalog();
            League first = new LeagueGenerator(new SquadGenerator(new PlayerGenerator(catalog)), catalog)
                .Generate(ClubCount, new SplitMix64RandomNumberGenerator(2024UL));
            League second = new LeagueGenerator(new SquadGenerator(new PlayerGenerator(catalog)), catalog)
                .Generate(ClubCount, new SplitMix64RandomNumberGenerator(2024UL));

            Assert.That(second.Name, Is.EqualTo(first.Name));
            var firstNames = new List<string>();
            var secondNames = new List<string>();
            foreach (Club club in first.Clubs)
            {
                firstNames.Add(club.Name);
            }

            foreach (Club club in second.Clubs)
            {
                secondNames.Add(club.Name);
            }

            Assert.That(secondNames, Is.EqualTo(firstNames));
            for (int i = 0; i < first.Clubs.Count; i++)
            {
                AssertSameStrength(second.Clubs[i].Strength, first.Clubs[i].Strength, first.Clubs[i].Name);
            }
        }

        private static void AssertSameStrength(TeamStrength actual, TeamStrength expected, string club)
        {
            Assert.That(actual.Attack, Is.EqualTo(expected.Attack).Within(1e-9), $"{club} attack");
            Assert.That(actual.Midfield, Is.EqualTo(expected.Midfield).Within(1e-9), $"{club} midfield");
            Assert.That(actual.Defence, Is.EqualTo(expected.Defence).Within(1e-9), $"{club} defence");
        }

        private static bool Same(double left, double right)
        {
            double difference = left - right;
            return difference < 1e-9 && difference > -1e-9;
        }
    }
}
