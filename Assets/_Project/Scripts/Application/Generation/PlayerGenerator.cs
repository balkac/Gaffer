using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Generates a plausible player: a name, nationality, position, age, visible attributes, and a
    /// hidden-potential ceiling — each drawn from the context's bands through the rng, so the same
    /// seed reproduces the same player. The guaranteed-wonderkid pass (low visible value, high hidden
    /// potential, lower-league, cheap) is a squad/pool-level step layered on this (TDD §5).
    /// </summary>
    public sealed class PlayerGenerator : IPlayerGenerator
    {
        private static readonly string[] Nationalities =
        {
            "England", "Scotland", "Wales", "Ireland", "France", "Spain", "Portugal", "Netherlands",
        };

        private const int PositionCount = 4;

        private readonly PlayerNameGenerator _names = new PlayerNameGenerator();

        public Player Generate(PlayerId id, GenerationContext context, IRandom rng)
        {
            string name = _names.GenerateName(rng);
            string nationality = Nationalities[rng.NextInt(Nationalities.Length)];
            int age = rng.NextInt(context.MinAge, context.MaxAge + 1);
            var position = (Position)rng.NextInt(PositionCount);
            return Build(id, context, name, nationality, age, position, rng);
        }

        /// <summary>
        /// Generates a player for a fixed role — the squad builder needs a believable line-up (a set
        /// number of keepers, defenders, and so on), not four uniformly-random positions. Draws the same
        /// fields as the random overload minus the position roll, so it stays deterministic in its own right.
        /// </summary>
        public Player Generate(PlayerId id, GenerationContext context, Position position, IRandom rng)
        {
            string name = _names.GenerateName(rng);
            string nationality = Nationalities[rng.NextInt(Nationalities.Length)];
            int age = rng.NextInt(context.MinAge, context.MaxAge + 1);
            return Build(id, context, name, nationality, age, position, rng);
        }

        // Keepers are poor with the ball at their feet; outfielders barely keep goal. These weak/negligible
        // bands are fixed, not tied to the club tier — a top keeper is still no striker, and vice versa.
        private const int WeakMin = 8;
        private const int WeakMax = 30;
        private const int NegligibleMin = 1;
        private const int NegligibleMax = 12;

        private Player Build(PlayerId id, GenerationContext context, string name, string nationality, int age, Position position, IRandom rng)
        {
            Attributes attributes = GenerateAttributes(context, position, rng);
            byte hiddenPotential = (byte)rng.NextInt(context.MinPotential, context.MaxPotential + 1);

            return new Player(id, name, nationality, position, age, attributes, hiddenPotential);
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
