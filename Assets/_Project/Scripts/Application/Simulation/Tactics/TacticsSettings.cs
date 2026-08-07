namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// How strongly each tactical axis bends the simulation — the balance behind <see cref="Tactics"/>
    /// (NON-NEGOTIABLE #3): the per-step strength multipliers mentality and pressing apply in
    /// <see cref="EffectiveStrengthBuilder"/>, and the volume/quality multipliers tempo and approach
    /// apply through <see cref="ChanceProfile.FromTactics(Tactics, TacticsSettings)"/>. Injectable and
    /// defaulted like every balance object; the authoring surface (`SimulationBalanceSO`) maps onto it.
    /// Immutable once built — <see cref="Default"/> is one shared cached instance, and get-only properties
    /// set by one all-optional constructor are what make that safe rather than merely intended. The
    /// defaults live in the constructor signature and are baked into every calling assembly at compile
    /// time, so a changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class TacticsSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new TacticsSettings(mentalityAttackStep: 0.12)</c>.
        /// </summary>
        public TacticsSettings(
            double mentalityAttackStep = 0.09,
            double pressingMidfieldStep = 0.09,
            double mentalityDefenceStep = 0.07,
            double pressingDefenceStep = 0.04,
            double intenseTempoVolume = 1.15,
            double patientTempoVolume = 0.87,
            double counterApproachVolume = 0.82,
            double counterApproachQuality = 1.20,
            double possessionApproachVolume = 1.15,
            double possessionApproachQuality = 0.88)
        {
            MentalityAttackStep = mentalityAttackStep;
            PressingMidfieldStep = pressingMidfieldStep;
            MentalityDefenceStep = mentalityDefenceStep;
            PressingDefenceStep = pressingDefenceStep;
            IntenseTempoVolume = intenseTempoVolume;
            PatientTempoVolume = patientTempoVolume;
            CounterApproachVolume = counterApproachVolume;
            CounterApproachQuality = counterApproachQuality;
            PossessionApproachVolume = possessionApproachVolume;
            PossessionApproachQuality = possessionApproachQuality;
        }

        /// <summary>Attack multiplier gained per mentality step (+2 very attacking … -2 very defensive).</summary>
        public double MentalityAttackStep { get; }

        /// <summary>Midfield multiplier gained per pressing step (+1 press … -1 contain).</summary>
        public double PressingMidfieldStep { get; }

        /// <summary>Defence multiplier lost per mentality step — attacking thins the line.</summary>
        public double MentalityDefenceStep { get; }

        /// <summary>Defence multiplier lost per pressing step — a high press exposes the line.</summary>
        public double PressingDefenceStep { get; }

        /// <summary>Chance-volume multiplier for an intense tempo.</summary>
        public double IntenseTempoVolume { get; }

        /// <summary>Chance-volume multiplier for a patient tempo.</summary>
        public double PatientTempoVolume { get; }

        /// <summary>Chance-volume multiplier for the counter — fewer chances…</summary>
        public double CounterApproachVolume { get; }

        /// <summary>…but sharper: chance-quality multiplier for the counter.</summary>
        public double CounterApproachQuality { get; }

        /// <summary>Chance-volume multiplier for possession — more chances…</summary>
        public double PossessionApproachVolume { get; }

        /// <summary>…but tamer: chance-quality multiplier for possession.</summary>
        public double PossessionApproachQuality { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new TacticsSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static TacticsSettings Default { get; } = new TacticsSettings();
    }
}
