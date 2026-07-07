using System;

namespace Gaffer.Common
{
    /// <summary>
    /// <see cref="IRandom"/> backed by the SplitMix64 algorithm: a single 64-bit state advanced by a
    /// fixed additive constant and finalized by a bijective mix. Fast, allocation-free, and fully
    /// determined by its seed — the same seed reproduces the same stream (NON-NEGOTIABLE #2). Named
    /// for the concrete algorithm, not a family (CONVENTIONS §2).
    /// </summary>
    public sealed class SplitMix64RandomNumberGenerator : IRandom
    {
        private const ulong GoldenGammaIncrement = 0x9E3779B97F4A7C15;
        private const ulong FirstMixMultiplier = 0xBF58476D1CE4E5B9;
        private const ulong SecondMixMultiplier = 0x94D049BB133111EB;

        private ulong _state;

        public SplitMix64RandomNumberGenerator(ulong seed)
        {
            _state = seed;
        }

        public ulong NextUInt64()
        {
            unchecked
            {
                _state += GoldenGammaIncrement;
                ulong z = _state;
                z = (z ^ (z >> 30)) * FirstMixMultiplier;
                z = (z ^ (z >> 27)) * SecondMixMultiplier;
                return z ^ (z >> 31);
            }
        }

        public int NextInt(int maxExclusive)
        {
            return NextInt(0, maxExclusive);
        }

        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (minInclusive >= maxExclusive)
            {
                throw new ArgumentException(
                    $"minInclusive ({minInclusive}) must be less than maxExclusive ({maxExclusive}).",
                    nameof(minInclusive));
            }

            ulong range = (ulong)((long)maxExclusive - minInclusive);

            // Rejection sampling removes modulo bias: discard the tail that does not divide evenly.
            ulong rejectionLimit = ulong.MaxValue - (ulong.MaxValue % range);
            ulong draw;
            do
            {
                draw = NextUInt64();
            }
            while (draw >= rejectionLimit);

            return (int)(minInclusive + (long)(draw % range));
        }

        public double NextDouble()
        {
            // Take the top 53 bits so every representable double in [0, 1) is reachable.
            return (NextUInt64() >> 11) * (1.0 / (1UL << 53));
        }
    }
}
