using Gaffer.Application.Transfers;
using UnityEngine;

namespace Gaffer.Infrastructure.Configuration
{
    /// <summary>
    /// The Unity authoring surface for the market's scale (NON-NEGOTIABLE #3): value and wage ceilings,
    /// rounding, and the age curve. Tune it in the Inspector and <see cref="ToSettings"/> maps to the
    /// pure <see cref="EconomySettings"/>. Config-as-override — no asset means
    /// <see cref="EconomySettings.Default"/>. Defaults mirror it.
    /// </summary>
    [CreateAssetMenu(menuName = "Gaffer/Balance/Economy", fileName = "EconomyBalance")]
    public sealed class EconomyBalanceSO : ScriptableObject
    {
        [Header("Valuation")]
        [Tooltip("Market value of a perfect (100-rated) player in his prime.")]
        [SerializeField] private double valuationCeiling = 40_000_000.0;

        [Tooltip("Values are rounded to this step.")]
        [SerializeField] private int valuationRounding = 50_000;

        [Tooltip("Age multipliers on value: <=18 / <=21 / <=27 / <=30 / <=32 / older.")]
        [SerializeField] private double valueFactorTo18 = 0.80;
        [SerializeField] private double valueFactorTo21 = 0.92;
        [SerializeField] private double valueFactorTo27 = 1.0;
        [SerializeField] private double valueFactorTo30 = 0.82;
        [SerializeField] private double valueFactorTo32 = 0.58;
        [SerializeField] private double valueFactorVeteran = 0.32;

        [Header("Wages")]
        [Tooltip("Weekly wage of a perfect (100-rated) player.")]
        [SerializeField] private double wageCeiling = 20_000.0;

        [Tooltip("Wages are rounded to this step.")]
        [SerializeField] private int wageRounding = 500;

        [Tooltip("No one plays for less than this per week.")]
        [SerializeField] private long wageFloor = 500;

        public EconomySettings ToSettings()
        {
            return new EconomySettings
            {
                ValuationCeiling = valuationCeiling,
                ValuationRounding = valuationRounding,
                ValueFactorTo18 = valueFactorTo18,
                ValueFactorTo21 = valueFactorTo21,
                ValueFactorTo27 = valueFactorTo27,
                ValueFactorTo30 = valueFactorTo30,
                ValueFactorTo32 = valueFactorTo32,
                ValueFactorVeteran = valueFactorVeteran,
                WageCeiling = wageCeiling,
                WageRounding = wageRounding,
                WageFloor = wageFloor,
            };
        }
    }
}
