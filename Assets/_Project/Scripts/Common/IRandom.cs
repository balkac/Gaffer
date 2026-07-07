namespace Gaffer.Common
{
    /// <summary>
    /// Injected source of deterministic randomness. The simulation draws every random value through
    /// this port so a given seed always reproduces the same match (NON-NEGOTIABLE #2): tests use a
    /// stub, the game uses a seeded generator, and the global <c>Random</c> is never touched.
    /// </summary>
    public interface IRandom
    {
        /// <summary>Returns the next raw 64-bit draw, uniform over the whole range.</summary>
        ulong NextUInt64();

        /// <summary>Returns an unbiased integer in [0, <paramref name="maxExclusive"/>).</summary>
        int NextInt(int maxExclusive);

        /// <summary>Returns an unbiased integer in [<paramref name="minInclusive"/>, <paramref name="maxExclusive"/>).</summary>
        int NextInt(int minInclusive, int maxExclusive);

        /// <summary>Returns a double in [0, 1).</summary>
        double NextDouble();
    }
}
