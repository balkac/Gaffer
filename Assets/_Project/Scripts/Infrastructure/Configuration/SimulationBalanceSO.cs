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

        public MatchSimulationSettings ToSettings()
        {
            return new MatchSimulationSettings(
                baseChancesPerTeam, meanChanceQuality, homeAdvantage, maxStrengthRatio,
                maxChanceQuality, chanceQualityVariance);
        }

        public TacticsSettings ToTacticsSettings()
        {
            return new TacticsSettings
            {
                MentalityAttackStep = mentalityAttackStep,
                PressingMidfieldStep = pressingMidfieldStep,
                MentalityDefenceStep = mentalityDefenceStep,
                PressingDefenceStep = pressingDefenceStep,
                IntenseTempoVolume = intenseTempoVolume,
                PatientTempoVolume = patientTempoVolume,
                CounterApproachVolume = counterApproachVolume,
                CounterApproachQuality = counterApproachQuality,
                PossessionApproachVolume = possessionApproachVolume,
                PossessionApproachQuality = possessionApproachQuality,
            };
        }

        public ScorerWeights ToScorerWeights()
        {
            return new ScorerWeights
            {
                MinOutfielderWeight = minOutfielderWeight,
                OpenPlayFinishing = openPlayFinishing,
                OpenPlayPositioning = openPlayPositioning,
                OpenPlayPace = openPlayPace,
                AerialHeading = aerialHeading,
                AerialJumping = aerialJumping,
                AerialStrength = aerialStrength,
                OpenPlayForward = openPlayForward,
                OpenPlayMidfielder = openPlayMidfielder,
                OpenPlayDefender = openPlayDefender,
                OpenPlayGoalkeeper = openPlayGoalkeeper,
                AerialForward = aerialForward,
                AerialMidfielder = aerialMidfielder,
                AerialDefender = aerialDefender,
                AerialGoalkeeper = aerialGoalkeeper,
            };
        }
    }
}
