using Gaffer.Domain.Traits;

namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// A trait's pull on an event's likelihood — the drama half of "a trait changes both the sim and
    /// event probability" (GDD §4.2). Declared on the event (single source per event; the trait
    /// authoring surface can mirror it): a press magnet multiplies a scandal's weight up, a loyal
    /// player multiplies a transfer request's down.
    /// </summary>
    public readonly struct DramaTraitBias
    {
        public DramaTraitBias(TraitId trait, double weightMultiplier)
        {
            Trait = trait;
            WeightMultiplier = weightMultiplier;
        }

        public TraitId Trait { get; }

        public double WeightMultiplier { get; }
    }
}
