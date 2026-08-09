using Gaffer.Common;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Decides whether a single chance becomes a goal — quality against finishing/keeper (TDD §6
    /// step 3). Behind a port so resolution is tuned independently of chance generation.
    /// </summary>
    public interface IChanceResolver
    {
        /// <summary>
        /// Whether this chance becomes a goal. Draws from <paramref name="rng"/>, so the call is ordered
        /// with respect to every other draw in the match.
        /// <para><b>Buffer lifetime (ARCHITECTURE §5a/§8a):</b> returns a value, holds no buffer, and
        /// keeps no reference to <paramref name="chance"/> past the call — so an implementation must not
        /// retain state across chances that a caller would have to know about.</para>
        /// </summary>
        bool ResolvesToGoal(Chance chance, IRandom rng);
    }
}
