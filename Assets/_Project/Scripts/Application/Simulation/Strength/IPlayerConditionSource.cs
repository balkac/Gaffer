using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Per-player transient condition (morale today; form later) read by the strength step — the
    /// port through which off-pitch state reaches the pitch (TDD §6.1: attribute + tactics + form +
    /// morale + traits). 1.0 means untouched.
    /// </summary>
    public interface IPlayerConditionSource
    {
        double RatingMultiplierOf(PlayerId id);
    }
}
