namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// The market's scale (NON-NEGOTIABLE #3): the value and wage ceilings, rounding, and the age curve
    /// that prices youth and decline. The *shape* (cubic value, quadratic wage — steep at the top) stays
    /// in <see cref="PlayerValuation"/>/<see cref="PlayerWage"/>; this object carries the numbers.
    /// Injectable and defaulted like every balance object; the authoring surface (`EconomyBalanceSO`)
    /// maps onto it. Immutable once built — <see cref="Default"/> is one shared cached
    /// instance, and <c>init</c> is what makes that safe rather than merely intended.
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>Market value of a perfect (100-rated) player in his prime.</summary>
        public double ValuationCeiling { get; init; } = 40_000_000.0;

        /// <summary>Values are rounded to this step.</summary>
        public int ValuationRounding { get; init; } = 50_000;

        /// <summary>Age multipliers on value: unproven youth a touch cheaper, the old much cheaper.</summary>
        public double ValueFactorTo18 { get; init; } = 0.80;

        public double ValueFactorTo21 { get; init; } = 0.92;

        public double ValueFactorTo27 { get; init; } = 1.0;

        public double ValueFactorTo30 { get; init; } = 0.82;

        public double ValueFactorTo32 { get; init; } = 0.58;

        public double ValueFactorVeteran { get; init; } = 0.32;

        /// <summary>Weekly wage of a perfect (100-rated) player.</summary>
        public double WageCeiling { get; init; } = 20_000.0;

        /// <summary>Wages are rounded to this step.</summary>
        public int WageRounding { get; init; } = 500;

        /// <summary>No one plays for less than this per week.</summary>
        public long WageFloor { get; init; } = 500;

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new EconomySettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static EconomySettings Default { get; } = new EconomySettings();
    }
}
