using Gaffer.Common;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Builds a player's name from his nationality's <see cref="PlayerNamePool"/>: a first name plus a
    /// surname assembled from a stem, an ending and a separator that may carry a nationality particle
    /// ("Ruud van der Meerveld", "Ciaran O'Quigley"). Names are invented, never copied from a real squad
    /// (PROGRESS decision #6), and they are data rather than localization keys.
    ///
    /// <para><b>Nationality first, then the name.</b> Drawing the name *from* the passport is what fixes
    /// the second half of the old defect: the pools used to be one generic English list, so an "Italian"
    /// could be called Harry Walker. A player now reads as being from where he says he is.</para>
    ///
    /// <para><b>Exactly two rng values per name</b>, whatever the nationality and whether or not a
    /// particle is drawn: one for the first name, one composite draw that a division and two moduli
    /// unpack into stem, ending and separator. A fixed draw count is what keeps a shared-stream pool
    /// reproducing identically seed for seed (the same discipline as the trait draw), and it is why
    /// this change leaves every generated player's attributes bit-identical to before.</para>
    ///
    /// <para><b>One allocation per name</b> — the four-part <see cref="string.Concat(string, string,
    /// string, string)"/>, which is inherent to producing a string. No LINQ, no per-call table
    /// building; the pools are <c>static readonly</c> (PERFORMANCE §8).</para>
    /// </summary>
    public sealed class PlayerNameGenerator
    {
        /// <summary>Generates a name fitting <paramref name="nationality"/>.</summary>
        public string GenerateName(string nationality, IRandom rng)
        {
            return GenerateName(PlayerNamePools.For(nationality), rng);
        }

        /// <summary>
        /// Generates a name from an already-resolved pool — the generation path, which draws the
        /// nationality itself and so has the pool in hand without a second lookup.
        /// </summary>
        public string GenerateName(PlayerNamePool pool, IRandom rng)
        {
            string first = pool.FirstNameAt(rng.NextInt(pool.FirstNameCount));

            // One draw over every (stem, ending, separator) slot, unpacked in place: uniform over the
            // slots, so repeating " " in a pool's separator array is what sets its particle rate.
            int roll = rng.NextInt(pool.SurnameSlotCount);
            int separator = roll % pool.SeparatorCount;
            roll /= pool.SeparatorCount;
            int ending = roll % pool.EndingCount;
            int stem = roll / pool.EndingCount;

            return string.Concat(first, pool.SeparatorAt(separator), pool.StemAt(stem), pool.EndingAt(ending));
        }
    }
}
