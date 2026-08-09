using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Pins the STRICTNESS POSTURE SPLIT (ARCHITECTURE §11) that the catalogs document, because a posture
    /// nothing tests is a comment. The two halves are deliberately opposite and both are asserted here so
    /// neither can be "tidied" into the other:
    /// <list type="bullet">
    /// <item>AUTHORED CONTENT (ships inside the build, asset and reader atomic, no acceptance policy to
    /// tune) is STRICT: a trait slug that resolves to nothing fails the load with the offending id named.
    /// Without this, a typo'd slug is invisible — every lookup site silently skips an unknown id, so the
    /// trait keeps its copy and loses its mechanics, which is the flavor-text failure NON-NEGOTIABLE #7
    /// exists to reject and which no other test can catch.</item>
    /// <item>PLAYER SAVE DATA (outlives the build) is TOLERANT: an unknown slug restores intact.</item>
    /// </list>
    /// The first test is the one that guards SHIPPED content: it runs the strict check over the built-in
    /// catalogs, so a dangling reference authored into <c>TraitCatalog.Default</c> or
    /// <c>DramaCatalog.Default</c> breaks CI on the commit that introduces it.
    /// </summary>
    public sealed class ContentCatalogValidationTests
    {
        [Test]
        public void Validate_TheShippedBuiltInCatalogs_HaveNoDanglingReferences()
        {
            Result traits = TraitCatalog.Default.Validate();
            Result drama = DramaCatalog.Default.ValidateAgainst(TraitCatalog.Default);

            Assert.That(traits.IsSuccess, Is.True, traits.Error);
            Assert.That(drama.IsSuccess, Is.True, drama.Error);
        }

        [Test]
        public void ValidateAgainst_BiasOnAnUndefinedTrait_FailsNamingTheTrait()
        {
            DramaCatalog catalog = CatalogWith(EventWith(
                subjectBiases: new[] { new DramaTraitBias(new TraitId("dressing-room-leaderr"), 0.5) }));

            Result result = catalog.ValidateAgainst(TraitCatalog.Default);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("dressing-room-leaderr"));
            Assert.That(result.Error, Does.Contain("typo-event"));
        }

        [Test]
        public void ValidateAgainst_SquadBiasOnAnUndefinedTrait_FailsNamingTheTrait()
        {
            DramaCatalog catalog = CatalogWith(EventWith(
                squadBiases: new[] { new DramaTraitBias(new TraitId("no-such-trait"), 2.0) }));

            Result result = catalog.ValidateAgainst(TraitCatalog.Default);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("no-such-trait"));
        }

        [Test]
        public void ValidateAgainst_TriggerRequiringAnUndefinedTrait_FailsNamingTheTrait()
        {
            // The nastiest of the four: an event gated on a trait nobody can have never fires, so the
            // symptom is content that simply never appears — nothing errors, ever.
            DramaCatalog catalog = CatalogWith(EventWith(
                trigger: new DramaTrigger { RequiredSubjectTrait = new TraitId("captain-material") }));

            Result result = catalog.ValidateAgainst(TraitCatalog.Default);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("captain-material"));
        }

        [Test]
        public void ValidateAgainst_ChoiceGrantingAnUndefinedTrait_FailsNamingTheTrait()
        {
            DramaCatalog catalog = CatalogWith(EventWith(
                grantedTrait: new TraitId("dressing-room-legend")));

            Result result = catalog.ValidateAgainst(TraitCatalog.Default);

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("dressing-room-legend"));
        }

        [Test]
        public void ValidateAgainst_EveryDanglingReference_IsReportedInOneMessage()
        {
            // One authoring pass fixes them all — a validator that stops at the first problem turns a
            // content review into a fix-run-fix loop.
            DramaCatalog catalog = CatalogWith(EventWith(
                subjectBiases: new[] { new DramaTraitBias(new TraitId("ghost-a"), 0.5) },
                grantedTrait: new TraitId("ghost-b")));

            Result result = catalog.ValidateAgainst(TraitCatalog.Default);

            Assert.That(result.Error, Does.Contain("ghost-a"));
            Assert.That(result.Error, Does.Contain("ghost-b"));
        }

        [Test]
        public void ValidateAgainst_BiasOnATraitTheAuthoredCatalogDoesDefine_Succeeds()
        {
            // "Dangling" is relative to the trait set the run actually plays with, not to the built-in one:
            // an authored catalog pairs with authored drama, and the check must accept that pairing.
            var authored = new TraitCatalog(new[]
            {
                new Trait(new TraitId("house-favourite"), "trait.house_favourite.name", 1.0, teammateAura: 1.05),
            });
            DramaCatalog catalog = CatalogWith(EventWith(
                subjectBiases: new[] { new DramaTraitBias(new TraitId("house-favourite"), 1.5) }));

            Assert.That(catalog.ValidateAgainst(authored).IsSuccess, Is.True);
            Assert.That(catalog.ValidateAgainst(TraitCatalog.Default).IsFailure, Is.True);
        }

        [Test]
        public void Validate_TraitCatalogWithADuplicateId_FailsNamingTheId()
        {
            // The second definition silently wins the lookup dictionary, so the first trait's mechanics
            // vanish with no symptom other than a player who behaves like someone else.
            var catalog = new TraitCatalog(new[]
            {
                new Trait(new TraitId("twin"), "trait.twin.name", 1.0, teammateAura: 1.1),
                new Trait(new TraitId("twin"), "trait.twin.name", 1.0, growthMultiplier: 0.5),
            });

            Result result = catalog.Validate();

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("twin"));
        }

        [Test]
        public void Validate_TraitWithNoSlug_Fails()
        {
            var catalog = new TraitCatalog(new[] { new Trait(default, "trait.nameless.name", 1.0) });

            Assert.That(catalog.Validate().IsFailure, Is.True);
        }

        [Test]
        public void Restore_SaveReferencingATraitThisBuildDoesNotDefine_KeepsTheSlug()
        {
            // The OPPOSITE posture, on purpose: a save outlives the build that wrote it, so an unknown
            // slug degrades and rebinds if the definition comes back — it must never fail the load the
            // way the authored-content path above does.
            var players = new List<Player>
            {
                PlayerWith(1, Position.Goalkeeper, "retired-from-this-build"),
                PlayerWith(2, Position.Defender, "derby-beast"),
            };
            var squad = new Squad(players);
            var league = new League("Tolerance League", new List<Club>
            {
                new Club(new ClubId(0), "Legacy FC", squad, new TeamStrength(55, 55, 55)),
                new Club(new ClubId(1), "Plain FC", new TeamStrength(55, 55, 55)),
            });

            var mapper = new SeasonSaveMapper();
            SeasonSaveData data = mapper.Capture(league, new LeagueSeason(league, null, null, null, null), 1UL, 1);
            RestoredSeason restored = mapper.Restore(data);

            IReadOnlyList<Player> restoredPlayers = restored.League.Clubs[0].Squad.Players;
            Assert.That(restoredPlayers[0].Traits, Is.EqualTo(new[] { new TraitId("retired-from-this-build") }));
            Assert.That(restoredPlayers[1].Traits, Is.EqualTo(new[] { new TraitId("derby-beast") }));
            Assert.That(TraitCatalog.Default.Find(new TraitId("retired-from-this-build")), Is.Null);
        }

        private static DramaCatalog CatalogWith(DramaEvent dramaEvent)
        {
            return new DramaCatalog(new[] { dramaEvent });
        }

        private static DramaEvent EventWith(
            IReadOnlyList<DramaTraitBias> subjectBiases = null,
            IReadOnlyList<DramaTraitBias> squadBiases = null,
            DramaTrigger trigger = null,
            TraitId grantedTrait = default)
        {
            var effects = new List<DramaEffect> { new DramaEffect(DramaEffectKind.TeamMorale, -1.0, 4) };
            if (!string.IsNullOrEmpty(grantedTrait.Value))
            {
                effects.Add(new DramaEffect(DramaEffectKind.GrantTraitToSuccessor, grantedTrait));
            }

            return new DramaEvent(
                new DramaEventId("typo-event"), DramaCategory.Personal,
                "drama.typo_event.title", "drama.typo_event.body",
                requiresSubject: true,
                trigger ?? new DramaTrigger(),
                baseWeight: 1.0, cooldownWeeks: 12,
                new[]
                {
                    new DramaChoice("drama.typo_event.first", effects),
                    new DramaChoice("drama.typo_event.second", System.Array.Empty<DramaEffect>()),
                },
                subjectBiases,
                squadBiases);
        }

        private static Player PlayerWith(int id, Position position, params string[] traits)
        {
            var attributes = new Attributes
            {
                Finishing = 60,
                Technique = 60,
                FirstTouch = 60,
                Dribbling = 60,
                Passing = 60,
                Crossing = 60,
                Heading = 60,
                LongShots = 60,
                Marking = 60,
                Tackling = 60,
                Penalties = 60,
                FreeKicks = 60,
                Corners = 60,
                LongThrows = 60,
                Pace = 60,
                Acceleration = 60,
                Stamina = 60,
                Strength = 60,
                Agility = 60,
                Jumping = 60,
                Balance = 60,
                Positioning = 60,
                Reflexes = 60,
                Handling = 60,
                AerialReach = 60,
                CommandOfArea = 60,
                OneOnOnes = 60,
                Kicking = 60,
                GkPositioning = 60,
            };

            var ids = new List<TraitId>(traits.Length);
            foreach (string trait in traits)
            {
                ids.Add(new TraitId(trait));
            }

            return new Player(new PlayerId(id), $"Player {id}", "England", position, 26, attributes, 75, ids);
        }
    }
}
