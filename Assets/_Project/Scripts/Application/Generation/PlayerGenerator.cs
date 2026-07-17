using System;
using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Generates a plausible player: a name, nationality, position, age, visible attributes, a
    /// hidden-potential ceiling, and a weighted-rare trait draw — each drawn from the context's bands
    /// through the rng, so the same seed reproduces the same player. The guaranteed-wonderkid pass
    /// (low visible value, high hidden potential, lower-league, cheap) is a squad/pool-level step
    /// layered on this (TDD §5). Trait definitions come from the injected catalog (data, not code).
    /// </summary>
    public sealed class PlayerGenerator : IPlayerGenerator
    {
        private static readonly string[] Nationalities =
        {
            "England", "Scotland", "Wales", "Ireland", "France", "Spain", "Portugal", "Netherlands",
        };

        private const int RoleCount = 12;

        private readonly PlayerNameGenerator _names = new PlayerNameGenerator();
        private readonly TraitCatalog _traits;

        public PlayerGenerator()
            : this(TraitCatalog.Default)
        {
        }

        public PlayerGenerator(TraitCatalog traits)
        {
            _traits = traits;
        }

        public Player Generate(PlayerId id, GenerationContext context, IRandom rng)
        {
            string name = _names.GenerateName(rng);
            string nationality = Nationalities[rng.NextInt(Nationalities.Length)];
            int age = rng.NextInt(context.MinAge, context.MaxAge + 1);
            var role = (PlayerRole)rng.NextInt(RoleCount);
            return Build(id, context, name, nationality, age, role, rng);
        }

        /// <summary>
        /// Generates a player for a fixed role — the squad builder needs a believable line-up (a keeper, a
        /// right-back, two strikers, and so on), not uniformly-random roles. Draws the same fields as the
        /// random overload minus the role roll, so it stays deterministic in its own right.
        /// </summary>
        public Player Generate(PlayerId id, GenerationContext context, PlayerRole role, IRandom rng)
        {
            string name = _names.GenerateName(rng);
            string nationality = Nationalities[rng.NextInt(Nationalities.Length)];
            int age = rng.NextInt(context.MinAge, context.MaxAge + 1);
            return Build(id, context, name, nationality, age, role, rng);
        }

        // Keepers are poor with the ball at their feet; outfielders barely keep goal. These weak/negligible
        // bands are fixed, not tied to the club tier — a top keeper is still no striker, and vice versa.
        private const int WeakMin = 8;
        private const int WeakMax = 30;
        private const int NegligibleMin = 1;
        private const int NegligibleMax = 12;

        private Player Build(PlayerId id, GenerationContext context, string name, string nationality, int age, PlayerRole role, IRandom rng)
        {
            Attributes attributes = GenerateAttributes(context, PlayerRoles.Line(role), rng);
            byte hiddenPotential = (byte)rng.NextInt(context.MinPotential, context.MaxPotential + 1);
            IReadOnlyList<TraitId> traits = DrawTraits(context, rng);

            return new Player(id, name, nationality, role, age, attributes, hiddenPotential, traits);
        }

        // Draws this player's traits: rare (most players carry none), weighted by each trait's authored
        // rarity, at most two. Always consumes exactly four rng values whatever the outcome, so the draw
        // count stays fixed and a shared-stream pool reproduces identically seed for seed.
        private IReadOnlyList<TraitId> DrawTraits(GenerationContext context, IRandom rng)
        {
            double hasAny = rng.NextDouble();
            double firstPick = rng.NextDouble();
            double hasSecond = rng.NextDouble();
            double secondPick = rng.NextDouble();

            IReadOnlyList<Trait> pool = _traits.Traits;
            if (pool.Count == 0 || hasAny >= context.TraitChance)
            {
                return Array.Empty<TraitId>();
            }

            Trait first = PickWeighted(pool, firstPick);
            if (hasSecond >= context.SecondTraitChance)
            {
                return new[] { first.Id };
            }

            Trait second = PickWeighted(pool, secondPick);
            if (second == first)
            {
                // Step to the next definition instead of redrawing, so the draw count stays fixed; with a
                // single-trait catalog there is no distinct second to give.
                if (pool.Count == 1)
                {
                    return new[] { first.Id };
                }

                second = pool[(IndexOf(pool, first) + 1) % pool.Count];
            }

            return new[] { first.Id, second.Id };
        }

        private static Trait PickWeighted(IReadOnlyList<Trait> pool, double roll)
        {
            double total = 0.0;
            foreach (Trait trait in pool)
            {
                total += trait.AssignmentWeight;
            }

            double target = roll * total;
            double cumulative = 0.0;
            foreach (Trait trait in pool)
            {
                cumulative += trait.AssignmentWeight;
                if (target < cumulative)
                {
                    return trait;
                }
            }

            return pool[pool.Count - 1];
        }

        private static int IndexOf(IReadOnlyList<Trait> pool, Trait trait)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == trait)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>
        /// Fills all attributes position-appropriately: physical &amp; movement in the club's ability band
        /// for everyone, technical and set-piece in band for outfielders but weak for keepers, and the
        /// goalkeeping group in band for keepers but negligible for outfielders. The draw count and order
        /// are fixed regardless of role, so a given seed and context still reproduce the same player.
        /// </summary>
        private static Attributes GenerateAttributes(GenerationContext context, Position position, IRandom rng)
        {
            bool isKeeper = position == Position.Goalkeeper;

            return new Attributes
            {
                // Technical — in band for outfielders, weak for keepers.
                Finishing = Technical(context, isKeeper, rng),
                Technique = Technical(context, isKeeper, rng),
                FirstTouch = Technical(context, isKeeper, rng),
                Dribbling = Technical(context, isKeeper, rng),
                Passing = Technical(context, isKeeper, rng),
                Crossing = Technical(context, isKeeper, rng),
                Heading = Technical(context, isKeeper, rng),
                LongShots = Technical(context, isKeeper, rng),
                Marking = Technical(context, isKeeper, rng),
                Tackling = Technical(context, isKeeper, rng),

                // Set-piece — in band for outfielders, weak for keepers.
                Penalties = Technical(context, isKeeper, rng),
                FreeKicks = Technical(context, isKeeper, rng),
                Corners = Technical(context, isKeeper, rng),
                LongThrows = Technical(context, isKeeper, rng),

                // Physical & movement — in band for everyone.
                Pace = InBand(context, rng),
                Acceleration = InBand(context, rng),
                Stamina = InBand(context, rng),
                Strength = InBand(context, rng),
                Agility = InBand(context, rng),
                Jumping = InBand(context, rng),
                Balance = InBand(context, rng),
                Positioning = InBand(context, rng),

                // Goalkeeping — in band for keepers, negligible for outfielders.
                Reflexes = Keeping(context, isKeeper, rng),
                Handling = Keeping(context, isKeeper, rng),
                AerialReach = Keeping(context, isKeeper, rng),
                CommandOfArea = Keeping(context, isKeeper, rng),
                OneOnOnes = Keeping(context, isKeeper, rng),
                Kicking = Keeping(context, isKeeper, rng),
                GkPositioning = Keeping(context, isKeeper, rng),
            };
        }

        private static byte Technical(GenerationContext context, bool isKeeper, IRandom rng)
        {
            return isKeeper ? (byte)rng.NextInt(WeakMin, WeakMax + 1) : InBand(context, rng);
        }

        private static byte Keeping(GenerationContext context, bool isKeeper, IRandom rng)
        {
            return isKeeper ? InBand(context, rng) : (byte)rng.NextInt(NegligibleMin, NegligibleMax + 1);
        }

        private static byte InBand(GenerationContext context, IRandom rng)
        {
            return (byte)rng.NextInt(context.MinAbility, context.MaxAbility + 1);
        }
    }
}
