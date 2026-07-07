using Gaffer.Common;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Resolves a chance by comparing a uniform draw against its xG-like quality: a low-quality chance
    /// usually misses, a high-quality one usually scores (TDD §6 step 3). Finishing/keeper interaction
    /// layers on once players feed the sim.
    /// </summary>
    public sealed class QualityChanceResolver : IChanceResolver
    {
        public bool ResolvesToGoal(Chance chance, IRandom rng)
        {
            return rng.NextDouble() < chance.Quality;
        }
    }
}
