using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// What playing a man out of position costs — one multiplier per <see cref="PositionalFit"/> tier
    /// (NON-NEGOTIABLE #3: the numbers are balance and live in an injectable settings object, the tiers
    /// are the model and live in the domain).
    ///
    /// <para><b>Where these numbers come from.</b> Football Manager's own out-of-position penalty, as
    /// measured rather than guessed: FM applies <c>rating × (1 − (20 − positionScore) / 46)</c>, about
    /// 2.17% per point of unfamiliarity, and FM-Arena's match-engine tests put the resulting tiers at
    /// roughly 10% down for Accomplished, 15% for Competent, 20% for Unconvincing, 35% for Awkward and 40%
    /// for Ineffectual — a full eleven of Ineffectual players fell from 2.1 to 1.2 points per game. Gaffer's
    /// tiers are mapped onto that: same line ≈ Accomplished, the next line along ≈ Unconvincing, two lines
    /// away ≈ Awkward, keeper-for-outfielder ≈ Ineffectual.</para>
    ///
    /// <para><b>Why the floor is 0.6 and not 0.</b> FM's own formula bottoms out near 0.57 and its worst
    /// tier still wins matches. A penalty that erased a player would make one misplaced name look like an
    /// injury crisis and would teach the manager to fear the team sheet rather than read it; the point is
    /// that a compromise is legible and costly, not that it is fatal. The auto-picker declines to volunteer
    /// an <see cref="PositionalFit.Impossible"/> pairing at all (<see cref="LineupSelector"/>), so this
    /// number is the price of a choice the manager made on purpose.</para>
    ///
    /// <para>Immutable once built, one cached shared <see cref="Default"/>, all-optional constructor — the
    /// convention every settings type in the core follows (<c>SettingsDefaultContractTests</c>).</para>
    /// </summary>
    public sealed class PositionalFitSettings
    {
        /// <summary>
        /// Every parameter is optional and defaulted to the calibrated value, so a caller writes only what
        /// it changes: <c>new PositionalFitSettings(sameLine: 0.95)</c>.
        /// </summary>
        public PositionalFitSettings(
            double sameLine = 0.90,
            double adjacentLine = 0.80,
            double distantLine = 0.68,
            double impossible = 0.60)
        {
            SameLine = sameLine;
            AdjacentLine = adjacentLine;
            DistantLine = distantLine;
            Impossible = impossible;
        }

        /// <summary>Another role on the same line — FM's "Accomplished", about 10% off.</summary>
        public double SameLine { get; }

        /// <summary>The next line along — FM's "Unconvincing", about 20% off.</summary>
        public double AdjacentLine { get; }

        /// <summary>Two lines or more — FM's "Awkward", about a third off.</summary>
        public double DistantLine { get; }

        /// <summary>Goalkeeper for outfielder, either way — FM's "Ineffectual", about 40% off.</summary>
        public double Impossible { get; }

        /// <summary>The calibrated defaults — one cached, shared, immutable instance (PERFORMANCE §8).</summary>
        public static PositionalFitSettings Default { get; } = new PositionalFitSettings();

        /// <summary>
        /// The rating multiplier for a fit. <see cref="PositionalFit.Natural"/> is exactly 1.0 and is not a
        /// setting: a player in his own role is what a role rating already means, so making it tunable would
        /// let a config asset silently rescale every rating in the game.
        /// </summary>
        public double MultiplierFor(PositionalFit fit)
        {
            switch (fit)
            {
                case PositionalFit.Natural:
                    return 1.0;
                case PositionalFit.SameLine:
                    return SameLine;
                case PositionalFit.AdjacentLine:
                    return AdjacentLine;
                case PositionalFit.DistantLine:
                    return DistantLine;
                default:
                    return Impossible;
            }
        }
    }
}
