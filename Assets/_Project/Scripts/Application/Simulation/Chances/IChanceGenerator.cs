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
        IReadOnlyList<Chance> GenerateChances(MatchCommand command, IRandom rng);
    }
}
