namespace Gaffer.Application.Transfers
{
    /// <summary>
    /// The scouting mask's calibration (NON-NEGOTIABLE #3): how wide a completely unscouted band is,
    /// for the hidden-potential estimate and for each key attribute. The *shape* (the band always
    /// contains the truth, its width shrinks linearly with accuracy, its midpoint is nudged off the
    /// truth) stays in <see cref="Scout"/>; this object carries the numbers, so the discovery fantasy
    /// can be retuned from a config asset instead of a literal in the algorithm.
    /// Immutable once built — <see cref="Default"/> is one shared cached instance, and get-only properties
    /// set by one all-optional constructor are what make that safe rather than merely intended. The
    /// defaults live in the constructor signature and are baked into every calling assembly at compile
    /// time, so a changed default needs a full recompile before it is live everywhere (see
    /// <c>SettingsDefaultContractTests</c>).
    /// </summary>
    public sealed class ScoutingSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new ScoutingSettings(potentialMaxWidth: 4)</c>.
        /// </summary>
        public ScoutingSettings(int potentialMaxWidth = 22, int attributeMaxWidth = 12)
        {
            PotentialMaxWidth = potentialMaxWidth;
            AttributeMaxWidth = attributeMaxWidth;
        }

        /// <summary>Half-width of a fully unscouted (accuracy 0) hidden-potential band, in rating points.</summary>
        public int PotentialMaxWidth { get; }

        /// <summary>Half-width of a fully unscouted (accuracy 0) attribute band, in rating points.</summary>
        public int AttributeMaxWidth { get; }

        /// <summary>
        /// The calibrated defaults — one cached, shared, immutable instance, the convention every
        /// settings type in the core follows. The auto-property initializer is what caches it;
        /// <c>=> new ScoutingSettings()</c> would be a method body and re-allocate on every read
        /// (PERFORMANCE §8).
        /// </summary>
        public static ScoutingSettings Default { get; } = new ScoutingSettings();
    }
}
