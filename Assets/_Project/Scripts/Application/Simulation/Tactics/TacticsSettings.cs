namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// How strongly each tactical axis bends the simulation — the balance behind <see cref="Tactics"/>
    /// (NON-NEGOTIABLE #3): the per-step strength multipliers mentality and pressing apply in
    /// <see cref="EffectiveStrengthBuilder"/>, and the volume/quality multipliers tempo and approach
    /// apply through <see cref="ChanceProfile.FromTactics(Tactics, TacticsSettings)"/>. Injectable and
    /// defaulted like every balance object; the authoring surface (`SimulationBalanceSO`) maps onto it.
    /// Immutable once built — <see cref="Default"/> is one shared cached
    /// instance, and <c>init</c> is what makes that safe rather than merely intended.
    /// </summary>
    public sealed class TacticsSettings
    {
        /// <summary>Attack multiplier gained per mentality step (+2 very attacking … -2 very defensive).</summary>
        public double MentalityAttackStep { get; init; } = 0.09;

        /// <summary>Midfield multiplier gained per pressing step (+1 press … -1 contain).</summary>
        public double PressingMidfieldStep { get; init; } = 0.09;

        /// <summary>Defence multiplier lost per mentality step — attacking thins the line.</summary>
        public double MentalityDefenceStep { get; init; } = 0.07;

        /// <summary>Defence multiplier lost per pressing step — a high press exposes the line.</summary>
        public double PressingDefenceStep { get; init; } = 0.04;

        /// <summary>Chance-volume multiplier for an intense tempo.</summary>
        public double IntenseTempoVolume { get; init; } = 1.15;

        /// <summary>Chance-volume multiplier for a patient tempo.</summary>
        public double PatientTempoVolume { get; init; } = 0.87;

        /// <summary>Chance-volume multiplier for the counter — fewer chances…</summary>
        public double CounterApproachVolume { get; init; } = 0.82;

        /// <summary>…but sharper: chance-quality multiplier for the counter.</summary>
        public double CounterApproachQuality { get; init; } = 1.20;

        /// <summary>Chance-volume multiplier for possession — more chances…</summary>
        public double PossessionApproachVolume { get; init; } = 1.15;

        /// <summary>…but tamer: chance-quality multiplier for possession.</summary>
        public double PossessionApproachQuality { get; init; } = 0.88;

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new TacticsSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static TacticsSettings Default { get; } = new TacticsSettings();
    }
}
