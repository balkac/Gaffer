using System;
using Gaffer.Application.Simulation;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for match-simulation balance (NON-NEGOTIABLE #3): tune the numbers in the
    /// Inspector, and <see cref="ToSettings"/> maps them to the pure <see cref="MatchSimulationSettings"/> the
    /// simulator already reads. Config-as-override — assign an asset to reshape scoring; assign none and the
    /// core uses <see cref="MatchSimulationSettings.Default"/> (the Gate A calibration). Defaults mirror it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Simulation", fileName = "SimulationBalance")]
    public sealed class SimulationBalanceSO : ScriptableObject
    {
        [Tooltip("Base scoring chances generated per team before strength and tactics adjust it.")]
        [SerializeField] private double baseChancesPerTeam = 7.0;

        [Tooltip("Mean probability a chance is taken — the main goals-per-match dial.")]
        [SerializeField] private double meanChanceQuality = 0.17;

        [Tooltip("Multiplier on the home side's chances.")]
        [SerializeField] private double homeAdvantage = 1.15;

        [Tooltip("Cap on how lopsided a strength mismatch can get, so upsets stay credible.")]
        [SerializeField] private double maxStrengthRatio = 2.0;

        [Tooltip("Hard cap on a single chance's conversion probability — no chance is a certainty.")]
        [SerializeField] private double maxChanceQuality = 0.95;

        [Tooltip("Half-width of the per-chance quality spread around the mean (0.5 = 0.5x-1.5x).")]
        [SerializeField] private double chanceQualityVariance = 0.5;

        [Header("Tactics — strength steps")]
        [Tooltip("Attack multiplier gained per mentality step.")]
        [SerializeField] private double mentalityAttackStep = 0.09;

        [Tooltip("Midfield multiplier gained per pressing step.")]
        [SerializeField] private double pressingMidfieldStep = 0.09;

        [Tooltip("Defence multiplier lost per mentality step — attacking thins the line.")]
        [SerializeField] private double mentalityDefenceStep = 0.07;

        [Tooltip("Defence multiplier lost per pressing step — a high press exposes the line.")]
        [SerializeField] private double pressingDefenceStep = 0.04;

        [Header("Tactics — chance profile")]
        [Tooltip("Chance-volume multiplier for an intense tempo.")]
        [SerializeField] private double intenseTempoVolume = 1.15;

        [Tooltip("Chance-volume multiplier for a patient tempo.")]
        [SerializeField] private double patientTempoVolume = 0.87;

        [Tooltip("Chance-volume multiplier for the counter (fewer chances...).")]
        [SerializeField] private double counterApproachVolume = 0.82;

        [Tooltip("...but sharper: chance-quality multiplier for the counter.")]
        [SerializeField] private double counterApproachQuality = 1.20;

        [Tooltip("Chance-volume multiplier for possession (more chances...).")]
        [SerializeField] private double possessionApproachVolume = 1.15;

        [Tooltip("...but tamer: chance-quality multiplier for possession.")]
        [SerializeField] private double possessionApproachQuality = 0.88;

        [Header("Scorer attribution")]
        [Tooltip("Floor keeping every outfielder a live threat; keepers are exempt.")]
        [SerializeField] private double minOutfielderWeight = 0.5;

        [Tooltip("Open play: finishing / positioning / pace weights.")]
        [SerializeField] private double openPlayFinishing = 0.6;
        [SerializeField] private double openPlayPositioning = 0.2;
        [SerializeField] private double openPlayPace = 0.2;

        [Tooltip("Aerial (set piece): heading / jumping / strength weights.")]
        [SerializeField] private double aerialHeading = 0.6;
        [SerializeField] private double aerialJumping = 0.25;
        [SerializeField] private double aerialStrength = 0.15;

        [Tooltip("Share of the open-play pathway per position (forward / midfielder / defender / keeper).")]
        [SerializeField] private double openPlayForward = 1.0;
        [SerializeField] private double openPlayMidfielder = 0.55;
        [SerializeField] private double openPlayDefender = 0.10;
        [SerializeField] private double openPlayGoalkeeper = 0.003;

        [Tooltip("Share of the aerial pathway per position (forward / midfielder / defender / keeper).")]
        [SerializeField] private double aerialForward = 0.45;
        [SerializeField] private double aerialMidfielder = 0.25;
        [SerializeField] private double aerialDefender = 0.30;
        [SerializeField] private double aerialGoalkeeper = 0.004;

        private void OnValidate()
        {
            ClampToValidRanges();
        }

        public MatchSimulationSettings ToSettings()
        {
            ClampToValidRanges();
            return new MatchSimulationSettings(
                baseChancesPerTeam, meanChanceQuality, homeAdvantage, maxStrengthRatio,
                maxChanceQuality, chanceQualityVariance);
        }

        public TacticsSettings ToTacticsSettings()
        {
            ClampToValidRanges();
            return new TacticsSettings(
                mentalityAttackStep: mentalityAttackStep,
                pressingMidfieldStep: pressingMidfieldStep,
                mentalityDefenceStep: mentalityDefenceStep,
                pressingDefenceStep: pressingDefenceStep,
                intenseTempoVolume: intenseTempoVolume,
                patientTempoVolume: patientTempoVolume,
                counterApproachVolume: counterApproachVolume,
                counterApproachQuality: counterApproachQuality,
                possessionApproachVolume: possessionApproachVolume,
                possessionApproachQuality: possessionApproachQuality);
        }

        public ScorerWeights ToScorerWeights()
        {
            ClampToValidRanges();
            return new ScorerWeights(
                minOutfielderWeight: minOutfielderWeight,
                openPlayFinishing: openPlayFinishing,
                openPlayPositioning: openPlayPositioning,
                openPlayPace: openPlayPace,
                aerialHeading: aerialHeading,
                aerialJumping: aerialJumping,
                aerialStrength: aerialStrength,
                openPlayForward: openPlayForward,
                openPlayMidfielder: openPlayMidfielder,
                openPlayDefender: openPlayDefender,
                openPlayGoalkeeper: openPlayGoalkeeper,
                aerialForward: aerialForward,
                aerialMidfielder: aerialMidfielder,
                aerialDefender: aerialDefender,
                aerialGoalkeeper: aerialGoalkeeper);
        }

        /// <summary>
        /// Holds every serialized number inside the band its consumer can actually make sense of
        /// (UNITY.md §8). Unity's <c>[Range]</c> only decorates float and int, and this core is
        /// double-based, so for these fields the clamp *is* the bound — and it is the only place that
        /// also catches a probability typed as an unbounded double (a <c>maxChanceQuality</c> of 9.5
        /// is a 40-goal match with no error anywhere). Called from <see cref="OnValidate"/> and from
        /// every mapper, because the attribute constrains the Inspector, not code that builds the
        /// settings object from a stale asset or a script. Every band is wider than the calibrated
        /// value it holds, so a shipping asset passes through untouched.
        /// </summary>
        private void ClampToValidRanges()
        {
            baseChancesPerTeam = Math.Clamp(baseChancesPerTeam, 0.0, 50.0);
            meanChanceQuality = Math.Clamp(meanChanceQuality, 0.0, 1.0);
            homeAdvantage = Math.Clamp(homeAdvantage, 0.5, 2.0);
            maxStrengthRatio = Math.Clamp(maxStrengthRatio, 1.0, 10.0);
            maxChanceQuality = Math.Clamp(maxChanceQuality, 0.0, 1.0);
            chanceQualityVariance = Math.Clamp(chanceQualityVariance, 0.0, 1.0);

            // A mentality/pressing step is applied at up to ±2 scale steps and is subtracted from 1 on
            // the defence axis, so anything at or above 0.5 would zero (or invert) a team's defence.
            mentalityAttackStep = Math.Clamp(mentalityAttackStep, 0.0, 0.4);
            pressingMidfieldStep = Math.Clamp(pressingMidfieldStep, 0.0, 0.4);
            mentalityDefenceStep = Math.Clamp(mentalityDefenceStep, 0.0, 0.4);
            pressingDefenceStep = Math.Clamp(pressingDefenceStep, 0.0, 0.4);

            intenseTempoVolume = Math.Clamp(intenseTempoVolume, 0.1, 3.0);
            patientTempoVolume = Math.Clamp(patientTempoVolume, 0.1, 3.0);
            counterApproachVolume = Math.Clamp(counterApproachVolume, 0.1, 3.0);
            counterApproachQuality = Math.Clamp(counterApproachQuality, 0.1, 3.0);
            possessionApproachVolume = Math.Clamp(possessionApproachVolume, 0.1, 3.0);
            possessionApproachQuality = Math.Clamp(possessionApproachQuality, 0.1, 3.0);

            // Scorer weights are relative shares: negative is meaningless, and the selector normalises
            // them, so the only real requirement is a non-negative, finite number.
            minOutfielderWeight = Math.Clamp(minOutfielderWeight, 0.0, 10.0);
            openPlayFinishing = Math.Clamp(openPlayFinishing, 0.0, 10.0);
            openPlayPositioning = Math.Clamp(openPlayPositioning, 0.0, 10.0);
            openPlayPace = Math.Clamp(openPlayPace, 0.0, 10.0);
            aerialHeading = Math.Clamp(aerialHeading, 0.0, 10.0);
            aerialJumping = Math.Clamp(aerialJumping, 0.0, 10.0);
            aerialStrength = Math.Clamp(aerialStrength, 0.0, 10.0);
            openPlayForward = Math.Clamp(openPlayForward, 0.0, 10.0);
            openPlayMidfielder = Math.Clamp(openPlayMidfielder, 0.0, 10.0);
            openPlayDefender = Math.Clamp(openPlayDefender, 0.0, 10.0);
            openPlayGoalkeeper = Math.Clamp(openPlayGoalkeeper, 0.0, 10.0);
            aerialForward = Math.Clamp(aerialForward, 0.0, 10.0);
            aerialMidfielder = Math.Clamp(aerialMidfielder, 0.0, 10.0);
            aerialDefender = Math.Clamp(aerialDefender, 0.0, 10.0);
            aerialGoalkeeper = Math.Clamp(aerialGoalkeeper, 0.0, 10.0);
        }
    }
}
