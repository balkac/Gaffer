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
        [SerializeField] private int maxEventsPerSeason = 4;

        [Tooltip("Weeks that must pass after any event before another may fire.")]
        [SerializeField] private int minWeeksBetweenEvents = 4;

        [Tooltip("Weekly firing probability per unit of candidate weight (trait biases scale frequency through this).")]
        [SerializeField] private double weeklyChancePerWeight = 0.10;

        [Tooltip("Ceiling on the weekly firing probability however heavy the candidates get.")]
        [SerializeField] private double maxWeeklyChance = 0.35;

        [Header("Morale")]
        [Tooltip("Rating multiplier delta per morale point (0.012 -> ±8 points is roughly ±10% form).")]
        [SerializeField] private double moraleRatingPerPoint = 0.012;

        [Tooltip("Clamp on the summed live morale points, in both directions.")]
        [SerializeField] private double moraleMaxAbsPoints = 8.0;

        public MoraleSettings ToMoraleSettings()
        {
            return new MoraleSettings
            {
                RatingPerPoint = moraleRatingPerPoint,
                MaxAbsPoints = moraleMaxAbsPoints,
            };
        }

        public DramaSettings ToSettings()
        {
            return new DramaSettings
            {
                MaxEventsPerSeason = maxEventsPerSeason,
                MinWeeksBetweenEvents = minWeeksBetweenEvents,
                WeeklyChancePerWeight = weeklyChancePerWeight,
                MaxWeeklyChance = maxWeeklyChance,
            };
        }
    }
}
