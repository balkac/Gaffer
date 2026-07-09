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

        public MatchSimulationSettings ToSettings()
        {
            return new MatchSimulationSettings(baseChancesPerTeam, meanChanceQuality, homeAdvantage, maxStrengthRatio);
        }
    }
}
