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
        PlayerId? SelectScorer(Squad squad, IRandom rng);
    }
}
