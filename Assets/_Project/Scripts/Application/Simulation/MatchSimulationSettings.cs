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

        /// <summary>
        /// The calibrated defaults. Deliberately the <c>=></c> form, and the one settings type in the
        /// core that keeps it — this is a <c>readonly struct</c>, so <c>new</c> allocates nothing
        /// (PERFORMANCE §8, "Free: <c>new</c> structs") and every read hands back an independent copy
        /// that cannot be aliased or written through. The class-shaped settings types must use
        /// <c>{ get; } = new X()</c> instead, because there <c>=></c> is a real heap allocation per
        /// access; see <c>SettingsDefaultContractTests</c> for the convention and why it splits here.
        /// Do not "fix" this into consistency with them.
        /// </summary>
        public static MatchSimulationSettings Default => new MatchSimulationSettings(7.0, 0.17, 1.15, 2.0);
    }
}
