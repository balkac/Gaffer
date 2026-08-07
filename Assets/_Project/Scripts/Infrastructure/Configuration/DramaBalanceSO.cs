using System;
using Gaffer.Application.Drama;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the drama frequency envelope (NON-NEGOTIABLE #3): the season
    /// budget, the minimum gap, and the weight-scaled weekly chance. Tune it in the Inspector and
    /// <see cref="ToSettings"/> maps to the pure <see cref="DramaSettings"/>. Config-as-override —
    /// no asset means <see cref="DramaSettings.Default"/>. Defaults mirror it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Drama", fileName = "DramaBalance")]
    public sealed class DramaBalanceSO : ScriptableObject
    {
        [Tooltip("Hard cap on events per season — scarcity keeps drama valuable.")]
        [Range(0, 52)] [SerializeField] private int maxEventsPerSeason = 4;

        [Tooltip("Weeks that must pass after any event before another may fire.")]
        [Range(0, 52)] [SerializeField] private int minWeeksBetweenEvents = 4;

        [Tooltip("Weekly firing probability per unit of candidate weight (trait biases scale frequency through this). A probability: 0-1.")]
        [SerializeField] private double weeklyChancePerWeight = 0.10;

        [Tooltip("Ceiling on the weekly firing probability however heavy the candidates get. A probability: 0-1.")]
        [SerializeField] private double maxWeeklyChance = 0.35;

        [Header("Morale")]
        [Tooltip("Rating multiplier delta per morale point (0.012 -> ±8 points is roughly ±10% form).")]
        [SerializeField] private double moraleRatingPerPoint = 0.012;

        [Tooltip("Clamp on the summed live morale points, in both directions.")]
        [SerializeField] private double moraleMaxAbsPoints = 8.0;

        private void OnValidate()
        {
            ClampToValidRanges();
        }

        public MoraleSettings ToMoraleSettings()
        {
            ClampToValidRanges();
            return new MoraleSettings
            {
                RatingPerPoint = moraleRatingPerPoint,
                MaxAbsPoints = moraleMaxAbsPoints,
            };
        }

        public DramaSettings ToSettings()
        {
            ClampToValidRanges();
            return new DramaSettings
            {
                MaxEventsPerSeason = maxEventsPerSeason,
                MinWeeksBetweenEvents = minWeeksBetweenEvents,
                WeeklyChancePerWeight = weeklyChancePerWeight,
                MaxWeeklyChance = maxWeeklyChance,
            };
        }

        /// <summary>
        /// Bounds the <c>double</c> fields Unity's <c>[Range]</c> cannot decorate (UNITY.md §8). Two of
        /// them are probabilities carried in an unbounded type, and the engine only compares them
        /// against a <c>NextDouble()</c> draw — so a value above 1 is not an error, it is drama every
        /// single week, silently. Called from the mappers too, because the attribute constrains the
        /// Inspector, not code that builds the settings object from a stale asset. Every band is wider
        /// than the calibrated value it holds.
        /// </summary>
        private void ClampToValidRanges()
        {
            weeklyChancePerWeight = Math.Clamp(weeklyChancePerWeight, 0.0, 1.0);
            maxWeeklyChance = Math.Clamp(maxWeeklyChance, 0.0, 1.0);
            moraleRatingPerPoint = Math.Clamp(moraleRatingPerPoint, 0.0, 1.0);
            moraleMaxAbsPoints = Math.Clamp(moraleMaxAbsPoints, 0.0, 100.0);
        }
    }
}
