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
    ///
    /// <para><b>The calibration rule for the chance-profile axes.</b> Tempo and approach each trade volume
    /// against quality, and each option's <c>volume × quality</c> product is held within ~2% of 1.0 on
    /// purpose: an option must change the <em>shape</em> of a side's chances, not its expected goals.
    /// The shipped products are approach 0.82×1.20 = 0.984 (counter) and 1.15×0.88 = 1.012 (possession);
    /// tempo 1.08×0.93 = 1.004 (intense) and 0.92×1.09 = 1.003 (patient). Move one half of a pair without
    /// the other and you are adding goal inflation to one setting and deflation to its opposite — which is
    /// what a tempo with no quality multiplier at all was: intense was 15% more chances for nothing.
    /// Tempo's swing is deliberately about half of approach's, so approach stays the coarse shape decision
    /// (±15-18% volume) and tempo the fine one (±8%); an exact mirror would make intense indistinguishable
    /// from possession and patient from the counter, collapsing two of the four axes into one.</para>
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
            double pressingDefenceStep = 0.08,
            double intenseTempoVolume = 1.08,
            double intenseTempoQuality = 0.93,
            double patientTempoVolume = 0.92,
            double patientTempoQuality = 1.09,
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
            IntenseTempoQuality = intenseTempoQuality;
            PatientTempoVolume = patientTempoVolume;
            PatientTempoQuality = patientTempoQuality;
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

        /// <summary>
        /// Defence multiplier lost per pressing step — a high press exposes the line. Calibrated
        /// against the possession the same step wins, not in isolation: pressing lifts the midfield by
        /// <see cref="PressingMidfieldStep"/>, which takes the ball off the opponent (his share of a
        /// 2/(2+step) split) and so *subtracts* ~4% from his chance count. A defence step that only
        /// gives that 4% back is not a cost at all — at 0.04 a high press measured +4.6% chances
        /// created against −0.9% conceded, i.e. strictly better than standing off on every metric.
        /// At 0.08 the exposure (+4.0% conceded) roughly matches the possession gain, which is what
        /// makes the press a decision (<c>NoFreeLunchTests</c>).
        /// </summary>
        public double PressingDefenceStep { get; }

        /// <summary>Chance-volume multiplier for an intense tempo — more chances…</summary>
        public double IntenseTempoVolume { get; }

        /// <summary>…but hurried: chance-quality multiplier for an intense tempo.</summary>
        public double IntenseTempoQuality { get; }

        /// <summary>Chance-volume multiplier for a patient tempo — fewer chances…</summary>
        public double PatientTempoVolume { get; }

        /// <summary>…but worked: chance-quality multiplier for a patient tempo.</summary>
        public double PatientTempoQuality { get; }

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
