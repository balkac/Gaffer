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
        public MatchSimulationSettings(double baseChancesPerTeam, double meanChanceQuality, double homeAdvantage, double maxStrengthRatio)
        {
            BaseChancesPerTeam = baseChancesPerTeam;
            MeanChanceQuality = meanChanceQuality;
            HomeAdvantage = homeAdvantage;
            MaxStrengthRatio = maxStrengthRatio;
        }

        public double BaseChancesPerTeam { get; }

        public double MeanChanceQuality { get; }

        public double HomeAdvantage { get; }

        public double MaxStrengthRatio { get; }

        public static MatchSimulationSettings Default => new MatchSimulationSettings(7.0, 0.20, 1.15, 2.0);
    }
}
