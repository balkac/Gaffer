namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The balance constants the match model reads, kept in one injected value so tuning changes the
    /// numbers, not the code (NON-NEGOTIABLE #3). These defaults are placeholders for Faz 1 tuning,
    /// where they move into an Infrastructure config asset and are calibrated against the believability
    /// targets (goals ~2.5–3/match, favourites usually win, upsets stay credible).
    /// </summary>
    public readonly struct MatchSimulationSettings
    {
        public MatchSimulationSettings(
            double baseChancesPerTeam,
            double meanChanceQuality,
            double homeAdvantage,
            double maxStrengthRatio,
            double maxChanceQuality = 0.95,
            double chanceQualityVariance = 0.5)
        {
            BaseChancesPerTeam = baseChancesPerTeam;
            MeanChanceQuality = meanChanceQuality;
            HomeAdvantage = homeAdvantage;
            MaxStrengthRatio = maxStrengthRatio;
            MaxChanceQuality = maxChanceQuality;
            ChanceQualityVariance = chanceQualityVariance;
        }

        public double BaseChancesPerTeam { get; }

        public double MeanChanceQuality { get; }

        public double HomeAdvantage { get; }

        public double MaxStrengthRatio { get; }

        /// <summary>Hard cap on a single chance's conversion probability — no chance is a certainty.</summary>
        public double MaxChanceQuality { get; }

        /// <summary>Half-width of the per-chance quality spread around the mean (0.5 → 0.5×–1.5×).</summary>
        public double ChanceQualityVariance { get; }

        public static MatchSimulationSettings Default => new MatchSimulationSettings(7.0, 0.17, 1.15, 2.0);
    }
}
