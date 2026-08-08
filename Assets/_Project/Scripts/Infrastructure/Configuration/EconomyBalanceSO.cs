using System;
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

        [Tooltip("Values are rounded to this step. It is a divisor — zero would make every player free.")]
        [Min(1)] [SerializeField] private int valuationRounding = 50_000;

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

        [Tooltip("Wages are rounded to this step. It is a divisor — zero would flatten every wage.")]
        [Min(1)] [SerializeField] private int wageRounding = 500;

        [Tooltip("No one plays for less than this per week.")]
        [SerializeField] private long wageFloor = 500;

        [Header("Budget exchange")]
        [Tooltip("Weeks of wage ceiling one lump of transfer cash is worth, both ways: giving up €1/wk of " +
            "ceiling pays this many € of cash, and the same many € buys €1/wk back. 38 = a season of match " +
            "weeks. One rate in both directions is what keeps a round trip neutral — split it and cycling " +
            "the budgets becomes an arbitrage.")]
        [Min(1)] [SerializeField] private int wageBudgetExchangeWeeks = 38;

        private void OnValidate()
        {
            ClampToValidRanges();
        }

        public EconomySettings ToSettings()
        {
            ClampToValidRanges();
            return new EconomySettings(
                valuationCeiling: valuationCeiling,
                valuationRounding: valuationRounding,
                valueFactorTo18: valueFactorTo18,
                valueFactorTo21: valueFactorTo21,
                valueFactorTo27: valueFactorTo27,
                valueFactorTo30: valueFactorTo30,
                valueFactorTo32: valueFactorTo32,
                valueFactorVeteran: valueFactorVeteran,
                wageCeiling: wageCeiling,
                wageRounding: wageRounding,
                wageFloor: wageFloor,
                wageBudgetExchangeWeeks: wageBudgetExchangeWeeks);
        }

        /// <summary>
        /// Bounds the fields <c>[Range]</c>/<c>[Min]</c> cannot reach — Unity's attributes only
        /// decorate float and int, and this surface is <c>double</c> and <c>long</c> (UNITY.md §8).
        /// The two rounding steps matter most: they are divisors, and a zero one does not throw — it
        /// makes the division infinite and collapses every price in the game (CONVENTIONS §6). Called
        /// from the mapper too, because an attribute constrains the Inspector, not code that builds the
        /// settings object from a stale asset. Every band is wider than the calibrated value it holds.
        /// </summary>
        private void ClampToValidRanges()
        {
            valuationCeiling = Math.Clamp(valuationCeiling, 0.0, 1e12);
            valuationRounding = Math.Max(1, valuationRounding);

            valueFactorTo18 = Math.Clamp(valueFactorTo18, 0.0, 5.0);
            valueFactorTo21 = Math.Clamp(valueFactorTo21, 0.0, 5.0);
            valueFactorTo27 = Math.Clamp(valueFactorTo27, 0.0, 5.0);
            valueFactorTo30 = Math.Clamp(valueFactorTo30, 0.0, 5.0);
            valueFactorTo32 = Math.Clamp(valueFactorTo32, 0.0, 5.0);
            valueFactorVeteran = Math.Clamp(valueFactorVeteran, 0.0, 5.0);

            wageCeiling = Math.Clamp(wageCeiling, 0.0, 1e9);
            wageRounding = Math.Max(1, wageRounding);
            wageFloor = Math.Max(0L, wageFloor);

            // A rate under one week is the third divisor-shaped hazard on this asset: it does not throw,
            // it makes giving up wage ceiling pay nothing while cash buys ceiling for free — a money pump
            // in both directions. Finances.ShiftWageBudget floors it at 1 as well, because an attribute
            // constrains the Inspector, not a settings object built from a stale asset.
            wageBudgetExchangeWeeks = Math.Max(1, wageBudgetExchangeWeeks);
        }
    }
}
