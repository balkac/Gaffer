namespace Gaffer.Presentation.Squad
{
    /// <summary>
    /// How loudly a number is shown — ART_STYLE §4.1's brightness ramp, as a value rather than a colour.
    ///
    /// <para><b>Bands, not colours, and that is the point.</b> The rule is "value magnitude reads as
    /// brightness against ONE accent" — the palette has a single signature colour and no second bright
    /// one (ART_STYLE §3). Naming the bands here keeps that rule in the pure layer, where it can be
    /// tested, and leaves the hexes in the stylesheet where a designer can change them without touching
    /// code. A band that carried a colour would put the palette in two places and quietly invite a second
    /// accent in through the back door.</para>
    /// </summary>
    public enum AbilityBand
    {
        /// <summary>Under 40 — present, barely. The dimmest step.</summary>
        Negligible = 0,

        /// <summary>40–54. Squad filler.</summary>
        Weak = 1,

        /// <summary>55–69. A player, not a problem.</summary>
        Fair = 2,

        /// <summary>70–84. Good enough to build on — the chalk step.</summary>
        Strong = 3,

        /// <summary>85 and up. The only band that burns accent, which is why it has to be rare.</summary>
        Elite = 4,
    }

    /// <summary>Reads a 0–100 number into the band it is shown at.</summary>
    public static class AbilityBands
    {
        /// <summary>
        /// The band for an attribute or a rating. The thresholds are ART_STYLE §4.1's, stated once here
        /// so every screen bands a number the same way — an attribute cell, a role rating and a scout
        /// row must agree, or the eye stops trusting brightness to mean anything.
        /// </summary>
        public static AbilityBand Of(double value)
        {
            if (value >= 85.0)
            {
                return AbilityBand.Elite;
            }

            if (value >= 70.0)
            {
                return AbilityBand.Strong;
            }

            if (value >= 55.0)
            {
                return AbilityBand.Fair;
            }

            return value >= 40.0 ? AbilityBand.Weak : AbilityBand.Negligible;
        }

        /// <summary>
        /// The USS class a band is drawn with. The stylesheet owns the colours; this owns only the name,
        /// so retuning the palette never touches C# and adding a sixth band cannot silently fall back to
        /// an unstyled cell.
        /// </summary>
        public static string ClassOf(AbilityBand band)
        {
            switch (band)
            {
                case AbilityBand.Elite:
                    return "value--elite";
                case AbilityBand.Strong:
                    return "value--strong";
                case AbilityBand.Fair:
                    return "value--fair";
                case AbilityBand.Weak:
                    return "value--weak";
                default:
                    return "value--negligible";
            }
        }
    }
}
