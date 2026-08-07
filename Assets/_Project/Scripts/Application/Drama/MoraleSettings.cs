namespace Gaffer.Application.Drama
{
    /// <summary>
    /// How hard morale bites on the pitch (NON-NEGOTIABLE #3): the rating multiplier per morale point
    /// and the clamp that keeps stacked drama from breaking the sim's believability. Injectable and
    /// defaulted like every balance object; the authoring surface (`DramaBalanceSO`) maps onto it.
    /// Immutable once built — <see cref="Default"/> is one shared cached instance, and get-only properties
    /// set by one all-optional constructor are what make that safe rather than merely intended. The
    /// defaults live in the constructor signature and are baked into every calling assembly at compile
    /// time, so a changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class MoraleSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new MoraleSettings(maxAbsPoints: 4.0)</c>.
        /// </summary>
        public MoraleSettings(double ratingPerPoint = 0.012, double maxAbsPoints = 8.0)
        {
            RatingPerPoint = ratingPerPoint;
            MaxAbsPoints = maxAbsPoints;
        }

        /// <summary>Rating multiplier delta per morale point (0.012 → ±8 points is roughly ±10% form).</summary>
        public double RatingPerPoint { get; }

        /// <summary>Clamp on the summed live morale points, in both directions.</summary>
        public double MaxAbsPoints { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows (spelled out in <c>SettingsDefaultContractTests</c>).
        /// The auto-property initializer is what caches it; <c>=> new MoraleSettings()</c> would be a
        /// method body and re-allocate on every read (PERFORMANCE §8).
        /// </summary>
        public static MoraleSettings Default { get; } = new MoraleSettings();
    }
}
