namespace Gaffer.Domain.Traits
{
    /// <summary>
    /// The context-conditional half of a trait: when a match carries any of the flagged stakes, the
    /// player's effective rating is scaled by <see cref="Multiplier"/> — a derby monster rises, a
    /// big-game bottler shrinks (GDD §4.2). Pure condition data; the simulation layer evaluates it
    /// against its match context, so the domain never reaches outward. <see cref="MatchStakes.None"/>
    /// means the trait has no match-day side.
    /// </summary>
    public readonly struct MatchTraitModifier
    {
        public MatchTraitModifier(MatchStakes stakes, double multiplier, int bigCrowdThreshold = 0)
        {
            Stakes = stakes;
            Multiplier = multiplier;
            BigCrowdThreshold = bigCrowdThreshold;
        }

        public static MatchTraitModifier None => default;

        public MatchStakes Stakes { get; }

        /// <summary>Applied once to the player's own rating when any flagged stake is present (not stacked per flag).</summary>
        public double Multiplier { get; }

        /// <summary>The crowd size at which <see cref="MatchStakes.BigCrowd"/> counts as big; 0 when not crowd-keyed.</summary>
        public int BigCrowdThreshold { get; }
    }
}
