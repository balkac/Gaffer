namespace Gaffer.Application.Drama
{
    /// <summary>
    /// How hard morale bites on the pitch (NON-NEGOTIABLE #3): the rating multiplier per morale point
    /// and the clamp that keeps stacked drama from breaking the sim's believability. Injectable and
    /// defaulted like every balance object; the authoring surface (`DramaBalanceSO`) maps onto it.
    /// Immutable once built — <see cref="Default"/> is one shared cached
    /// instance, and <c>init</c> is what makes that safe rather than merely intended.
    /// </summary>
    public sealed class MoraleSettings
    {
        /// <summary>Rating multiplier delta per morale point (0.012 → ±8 points is roughly ±10% form).</summary>
        public double RatingPerPoint { get; init; } = 0.012;

        /// <summary>Clamp on the summed live morale points, in both directions.</summary>
        public double MaxAbsPoints { get; init; } = 8.0;

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new MoraleSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static MoraleSettings Default { get; } = new MoraleSettings();
    }
}
