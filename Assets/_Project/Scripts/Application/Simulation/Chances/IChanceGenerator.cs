using System.Collections.Generic;
using Gaffer.Common;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Generates the goal-scoring chances a match produces from the two sides' strengths and context
    /// (TDD §6 step 2). Behind a port so tuning swaps the implementation while the pipeline stays
    /// fixed (ARCHITECTURE §9).
    /// </summary>
    public interface IChanceGenerator
    {
        /// <summary>
        /// The chances this match produces, in generation order.
        /// <para><b>Buffer lifetime — part of this port's contract, not an implementation detail
        /// (ARCHITECTURE §5a/§8a).</b> An implementation may return a scratch buffer it reuses, so the
        /// returned list is only guaranteed <b>valid until the next <see cref="GenerateChances"/> call on
        /// the same instance</b>. Callers consume it synchronously and copy out anything they keep; a
        /// caller that stores the reference will silently see the next match's chances.</para>
        /// </summary>
        IReadOnlyList<Chance> GenerateChances(MatchCommand command, IRandom rng);
    }
}
