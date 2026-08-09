namespace Gaffer.Application.Generation
{
    /// <summary>
    /// One nationality's name material: a first-name list plus a surname *grammar* — stems that combine
    /// with endings, optionally carried by a separator that glues a nationality particle onto the
    /// surname ("van der", "Mac", "O'", "da"). The grammar is what makes the pool scale: 56 stems and 12
    /// endings author 672 surnames from 68 authored strings, and every surname is invented rather than
    /// copied from a real squad list (PROGRESS decision #6).
    ///
    /// <para>The pool is pure data — it is drawn from by <see cref="PlayerNameGenerator"/>, which owns
    /// the draw. Every array is <c>static readonly</c> material handed in once at type initialisation
    /// (PERFORMANCE §8), so a pool costs nothing per generated player.</para>
    ///
    /// <para><b>Separators carry their own spacing.</b> A bare surname uses <c>" "</c>; a particled one
    /// uses <c>" van der "</c> or <c>" Mac"</c>. That keeps the name a single four-part concatenation
    /// (first + separator + stem + ending) whether or not a particle is drawn — one string allocation
    /// per player, which is inherent. Repeating <c>" "</c> in the separator array is how a pool weights
    /// its particle rate: four particles among nine slots is a 44% rate.</para>
    /// </summary>
    public sealed class PlayerNamePool
    {
        private readonly string[] _firstNames;
        private readonly string[] _stems;
        private readonly string[] _endings;
        private readonly string[] _separators;
        private readonly int _distinctSeparators;

        public PlayerNamePool(string nationality, string[] firstNames, string[] stems, string[] endings, string[] separators)
        {
            Nationality = nationality;
            _firstNames = firstNames;
            _stems = stems;
            _endings = endings;
            _separators = separators;
            _distinctSeparators = CountDistinct(separators);
        }

        /// <summary>The nationality this material belongs to — a player's name and passport agree.</summary>
        public string Nationality { get; }

        public int FirstNameCount => _firstNames.Length;

        public int StemCount => _stems.Length;

        public int EndingCount => _endings.Length;

        public int SeparatorCount => _separators.Length;

        /// <summary>
        /// The surname draw's range: every (stem, ending, separator) slot, so one rng value decides the
        /// whole surname. Weighted slots are included — the draw is uniform over slots, not over
        /// distinct surnames, which is what gives the particle rate its weighting.
        /// </summary>
        public int SurnameSlotCount => _stems.Length * _endings.Length * _separators.Length;

        /// <summary>
        /// How many different full names this pool can produce — first names times distinct surnames.
        /// The believability ceiling the old 28x28 pool hit at 784; this is what the distinctness test
        /// reasons about.
        /// </summary>
        public long DistinctNameCount =>
            (long)_firstNames.Length * _stems.Length * _endings.Length * _distinctSeparators;

        public string FirstNameAt(int index) => _firstNames[index];

        public string StemAt(int index) => _stems[index];

        public string EndingAt(int index) => _endings[index];

        public string SeparatorAt(int index) => _separators[index];

        private static int CountDistinct(string[] values)
        {
            int distinct = 0;
            for (int i = 0; i < values.Length; i++)
            {
                bool seen = false;
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(values[i], values[j], System.StringComparison.Ordinal))
                    {
                        seen = true;
                        break;
                    }
                }

                if (!seen)
                {
                    distinct++;
                }
            }

            return distinct;
        }
    }
}
