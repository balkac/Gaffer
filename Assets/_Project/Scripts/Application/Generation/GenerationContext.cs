namespace Gaffer.Application.Generation
{
    /// <summary>
    /// The bands a player is generated within — age, visible ability, and hidden-potential ceiling.
    /// Overridable so different tiers (a top club vs a lower-league side) or the guaranteed-wonderkid
    /// pass (TDD §5) can widen or shift the ranges. Deterministic values live in the rng, not here.
    /// </summary>
    public sealed class GenerationContext
    {
        public int MinAge { get; set; } = 16;

        public int MaxAge { get; set; } = 34;

        public byte MinAbility { get; set; } = 40;

        public byte MaxAbility { get; set; } = 72;

        public byte MinPotential { get; set; } = 55;

        public byte MaxPotential { get; set; } = 90;
    }
}
