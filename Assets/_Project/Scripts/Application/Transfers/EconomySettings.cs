namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// The market's scale (NON-NEGOTIABLE #3): the value and wage ceilings, rounding, and the age curve
    /// that prices youth and decline. The *shape* (cubic value, quadratic wage — steep at the top) stays
    /// in <see cref="PlayerValuation"/>/<see cref="PlayerWage"/>; this object carries the numbers.
    /// Injectable and defaulted like every balance object; the authoring surface (`EconomyBalanceSO`)
    /// maps onto it. Immutable once built — <see cref="Default"/> is one shared cached instance, and
    /// get-only properties set by one all-optional constructor are what make that safe rather than merely
    /// intended. The defaults live in the constructor signature and are baked into every calling assembly
    /// at compile time, so a changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class EconomySettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new EconomySettings(valuationRounding: 1)</c>.
        /// </summary>
        public EconomySettings(
            double valuationCeiling = 40_000_000.0,
            int valuationRounding = 50_000,
            double valueFactorTo18 = 0.80,
            double valueFactorTo21 = 0.92,
            double valueFactorTo27 = 1.0,
            double valueFactorTo30 = 0.82,
            double valueFactorTo32 = 0.58,
            double valueFactorVeteran = 0.32,
            double wageCeiling = 20_000.0,
            int wageRounding = 500,
            long wageFloor = 500)
        {
            ValuationCeiling = valuationCeiling;
            ValuationRounding = valuationRounding;
            ValueFactorTo18 = valueFactorTo18;
            ValueFactorTo21 = valueFactorTo21;
            ValueFactorTo27 = valueFactorTo27;
            ValueFactorTo30 = valueFactorTo30;
            ValueFactorTo32 = valueFactorTo32;
            ValueFactorVeteran = valueFactorVeteran;
            WageCeiling = wageCeiling;
            WageRounding = wageRounding;
            WageFloor = wageFloor;
        }

        /// <summary>Market value of a perfect (100-rated) player in his prime.</summary>
        public double ValuationCeiling { get; }

        /// <summary>Values are rounded to this step.</summary>
        public int ValuationRounding { get; }

        /// <summary>Age multipliers on value: unproven youth a touch cheaper, the old much cheaper.</summary>
        public double ValueFactorTo18 { get; }

        public double ValueFactorTo21 { get; }

        public double ValueFactorTo27 { get; }

        public double ValueFactorTo30 { get; }

        public double ValueFactorTo32 { get; }

        public double ValueFactorVeteran { get; }

        /// <summary>Weekly wage of a perfect (100-rated) player.</summary>
        public double WageCeiling { get; }

        /// <summary>Wages are rounded to this step.</summary>
        public int WageRounding { get; }

        /// <summary>No one plays for less than this per week.</summary>
        public long WageFloor { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new EconomySettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static EconomySettings Default { get; } = new EconomySettings();
    }
}
