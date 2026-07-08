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
            Attributes attributes = GenerateAttributes(context, rng);
            byte hiddenPotential = (byte)rng.NextInt(context.MinPotential, context.MaxPotential + 1);

            return new Player(id, name, nationality, position, age, attributes, hiddenPotential);
        }

        private static Attributes GenerateAttributes(GenerationContext context, IRandom rng)
        {
            return new Attributes(
                GenerateStat(context, rng),
                GenerateStat(context, rng),
                GenerateStat(context, rng),
                GenerateStat(context, rng),
                GenerateStat(context, rng),
                GenerateStat(context, rng));
        }

        private static byte GenerateStat(GenerationContext context, IRandom rng)
        {
            return (byte)rng.NextInt(context.MinAbility, context.MaxAbility + 1);
        }
    }
}
