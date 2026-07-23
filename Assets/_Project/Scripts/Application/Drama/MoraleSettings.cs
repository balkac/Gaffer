namespace Gaffer.Application.Drama
{
    /// <summary>
    /// How hard morale bites on the pitch (NON-NEGOTIABLE #3): the rating multiplier per morale point
    /// and the clamp that keeps stacked drama from breaking the sim's believability. Injectable and
    /// defaulted like every balance object; the authoring surface (`DramaBalanceSO`) maps onto it.
    /// Treat an instance as immutable once built — <see cref="Default"/> is shared.
    /// </summary>
    public sealed class MoraleSettings
    {
        /// <summary>Rating multiplier delta per morale point (0.012 → ±8 points is roughly ±10% form).</summary>
        public double RatingPerPoint { get; set; } = 0.012;

        /// <summary>Clamp on the summed live morale points, in both directions.</summary>
        public double MaxAbsPoints { get; set; } = 8.0;

        /// <summary>The calibrated defaults — cached; what the core uses when no config asset overrides them.</summary>
        public static MoraleSettings Default { get; } = new MoraleSettings();
    }
}
