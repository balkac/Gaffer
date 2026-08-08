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
            long wageFloor = 500,
            int wageBudgetExchangeWeeks = 38)
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
            WageBudgetExchangeWeeks = wageBudgetExchangeWeeks;
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
        /// The board's rate between the two budgets (<see cref="Finances.ShiftWageBudget(long, EconomySettings)"/>):
        /// how many weeks of wage ceiling one lump of transfer cash is worth. Giving up €1/wk of ceiling
        /// pays this many € of cash, and the same many € of cash buys €1/wk back — <b>one rate, both
        /// directions</b>, which is what makes a round trip exactly neutral and leaves no arbitrage.
        ///
        /// <para>38 is a season's worth of match weeks: a 20-club league is a 38-round double round robin,
        /// so a wage freed for the season is worth the 38 payments it saves. The season length itself is
        /// not a constant anywhere in the core — it is derived per league from the club count
        /// (<c>LeagueSeason.RoundCount</c>), and deriving the rate from the <em>current</em> league would
        /// make the exchange cheaper in a small league and reprice it mid-run at a promotion. It is a
        /// board term, so it is balance data here rather than a fact about a fixture list.</para>
        /// </summary>
        public int WageBudgetExchangeWeeks { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new EconomySettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static EconomySettings Default { get; } = new EconomySettings();
    }
}
