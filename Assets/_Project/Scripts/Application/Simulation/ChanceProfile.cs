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
            double volume = TempoVolume(tactics.Tempo) * ApproachVolume(tactics.Approach);
            double quality = ApproachQuality(tactics.Approach);
            return new ChanceProfile(volume, quality);
        }

        private static double TempoVolume(Tempo tempo)
        {
            switch (tempo)
            {
                case Tempo.Intense:
                    return 1.15;
                case Tempo.Patient:
                    return 0.87;
                default:
                    return 1.0;
            }
        }

        private static double ApproachVolume(Approach approach)
        {
            switch (approach)
            {
                case Approach.Counter:
                    return 0.82;
                case Approach.Possession:
                    return 1.15;
                default:
                    return 1.0;
            }
        }

        private static double ApproachQuality(Approach approach)
        {
            switch (approach)
            {
                case Approach.Counter:
                    return 1.20;
                case Approach.Possession:
                    return 0.88;
                default:
                    return 1.0;
            }
        }
    }
}
