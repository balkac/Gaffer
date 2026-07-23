namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// The market's scale (NON-NEGOTIABLE #3): the value and wage ceilings, rounding, and the age curve
    /// that prices youth and decline. The *shape* (cubic value, quadratic wage — steep at the top) stays
    /// in <see cref="PlayerValuation"/>/<see cref="PlayerWage"/>; this object carries the numbers.
    /// Injectable and defaulted like every balance object; the authoring surface (`EconomyBalanceSO`)
    /// maps onto it. Treat an instance as immutable once built — <see cref="Default"/> is shared.
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>Market value of a perfect (100-rated) player in his prime.</summary>
        public double ValuationCeiling { get; set; } = 40_000_000.0;

        /// <summary>Values are rounded to this step.</summary>
        public int ValuationRounding { get; set; } = 50_000;

        /// <summary>Age multipliers on value: unproven youth a touch cheaper, the old much cheaper.</summary>
        public double ValueFactorTo18 { get; set; } = 0.80;

        public double ValueFactorTo21 { get; set; } = 0.92;

        public double ValueFactorTo27 { get; set; } = 1.0;

        public double ValueFactorTo30 { get; set; } = 0.82;

        public double ValueFactorTo32 { get; set; } = 0.58;

        public double ValueFactorVeteran { get; set; } = 0.32;

        /// <summary>Weekly wage of a perfect (100-rated) player.</summary>
        public double WageCeiling { get; set; } = 20_000.0;

        /// <summary>Wages are rounded to this step.</summary>
        public int WageRounding { get; set; } = 500;

        /// <summary>No one plays for less than this per week.</summary>
        public long WageFloor { get; set; } = 500;

        /// <summary>The calibrated defaults — cached; what the core uses when no config asset overrides them.</summary>
        public static EconomySettings Default { get; } = new EconomySettings();
    }
}
