namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// How a side's tactics reshape its chances: a volume multiplier on how many it creates and a quality
    /// multiplier on how dangerous each one is (TDD §6). Tempo drives volume; the approach trades volume
    /// for quality — the counter makes fewer but sharper chances, possession more but tamer ones. A
    /// <see cref="Neutral"/> profile changes nothing, so strength-only matches are unaffected.
    /// </summary>
    public readonly struct ChanceProfile
    {
        public ChanceProfile(double volume, double quality)
        {
            Volume = volume;
            Quality = quality;
        }

        public double Volume { get; }

        public double Quality { get; }

        public static ChanceProfile Neutral => new ChanceProfile(1.0, 1.0);

        public static ChanceProfile FromTactics(Tactics tactics)
        {
            return FromTactics(tactics, TacticsSettings.Default);
        }

        /// <summary>Derives the profile with specific tactics balance (from a config asset).</summary>
        public static ChanceProfile FromTactics(Tactics tactics, TacticsSettings settings)
        {
            double volume = TempoVolume(tactics.Tempo, settings) * ApproachVolume(tactics.Approach, settings);
            double quality = ApproachQuality(tactics.Approach, settings);
            return new ChanceProfile(volume, quality);
        }

        private static double TempoVolume(Tempo tempo, TacticsSettings settings)
        {
            switch (tempo)
            {
                case Tempo.Intense:
                    return settings.IntenseTempoVolume;
                case Tempo.Patient:
                    return settings.PatientTempoVolume;
                default:
                    return 1.0;
            }
        }

        private static double ApproachVolume(Approach approach, TacticsSettings settings)
        {
            switch (approach)
            {
                case Approach.Counter:
                    return settings.CounterApproachVolume;
                case Approach.Possession:
                    return settings.PossessionApproachVolume;
                default:
                    return 1.0;
            }
        }

        private static double ApproachQuality(Approach approach, TacticsSettings settings)
        {
            switch (approach)
            {
                case Approach.Counter:
                    return settings.CounterApproachQuality;
                case Approach.Possession:
                    return settings.PossessionApproachQuality;
                default:
                    return 1.0;
            }
        }
    }
}
