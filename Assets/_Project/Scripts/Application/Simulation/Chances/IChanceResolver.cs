using Gaffer.Common;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Decides whether a single chance becomes a goal — quality against finishing/keeper (TDD §6
    /// step 3). Behind a port so resolution is tuned independently of chance generation.
    /// </summary>
    public interface IChanceResolver
    {
        bool ResolvesToGoal(Chance chance, IRandom rng);
    }
}
