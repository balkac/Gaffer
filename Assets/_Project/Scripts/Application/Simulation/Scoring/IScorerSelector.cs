using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Picks which player is credited with a goal — the step that turns a side-attributed goal into a
    /// named beat (TDD §4.1 step 6). Behind a port so the weighting is tuned independently of the sim.
    /// Returns null when there is no squad to draw from, leaving the goal unnamed.
    /// </summary>
    public interface IScorerSelector
    {
        /// <summary>
        /// The player credited with this goal, or null when there is no squad to draw from. Consumes
        /// exactly one draw from <paramref name="rng"/> when a squad is given and none when it is not,
        /// so a strength-only match leaves the stream untouched.
        /// <para><b>Buffer lifetime (ARCHITECTURE §5a/§8a):</b> returns a value, so nothing is borrowed.
        /// An implementation may memoise per-squad work between calls, which makes
        /// <paramref name="squad"/>'s immutability part of this port's contract: hand over a
        /// <see cref="Squad"/> instance whose roster will never change, and a changed roster as a new
        /// instance.</para>
        /// </summary>
        PlayerId? SelectScorer(IReadOnlyList<Player> onThePitch, IRandom rng);
    }
}
