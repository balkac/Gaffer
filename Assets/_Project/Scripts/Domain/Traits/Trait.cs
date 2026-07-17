namespace Gaffer.Domain.Traits
{
    /// <summary>
    /// A pure trait definition — the mechanical hooks that make a player a character instead of a
    /// stat sheet (GDD §4.2). Every field is a real lever on the simulation, never flavor text
    /// (NON-NEGOTIABLE #7): the match modifier moves this player's effective rating with the stakes,
    /// the aura moves his teammates', and the development fields bend his growth and decline curves.
    /// Definitions are data — authored in an Infrastructure asset and mapped to this type, with a
    /// built-in default catalog as the override base (<see cref="TraitCatalog.Default"/>).
    /// </summary>
    public sealed class Trait
    {
        public Trait(
            TraitId id,
            string nameKey,
            double assignmentWeight,
            MatchTraitModifier match = default,
            double teammateAura = 1.0,
            double growthMultiplier = 1.0,
            int declineOnsetShift = 0,
            double declineRateMultiplier = 1.0)
        {
            Id = id;
            NameKey = nameKey;
            AssignmentWeight = assignmentWeight;
            Match = match;
            TeammateAura = teammateAura;
            GrowthMultiplier = growthMultiplier;
            DeclineOnsetShift = declineOnsetShift;
            DeclineRateMultiplier = declineRateMultiplier;
        }

        public TraitId Id { get; }

        /// <summary>Localization key for the display name (NON-NEGOTIABLE #8) — never literal user text.</summary>
        public string NameKey { get; }

        /// <summary>Relative weight the generator draws this trait with — rarity is authored, not hardcoded.</summary>
        public double AssignmentWeight { get; }

        /// <summary>Context-conditional rating scale on the player himself (derby monster, bottler, showman).</summary>
        public MatchTraitModifier Match { get; }

        /// <summary>Rating scale on every teammate while he is in the eleven (dressing-room leader); 1.0 = none.</summary>
        public double TeammateAura { get; }

        /// <summary>Scale on seasonal growth toward potential (training dodger below 1, professional above); 1.0 = neutral.</summary>
        public double GrowthMultiplier { get; }

        /// <summary>Years added to this player's decline onset age (glass man negative, professional positive).</summary>
        public int DeclineOnsetShift { get; }

        /// <summary>Scale on the post-peak erosion once decline starts (glass man wears faster); 1.0 = neutral.</summary>
        public double DeclineRateMultiplier { get; }
    }
}
