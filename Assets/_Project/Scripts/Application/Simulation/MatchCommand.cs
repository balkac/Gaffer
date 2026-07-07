using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The immutable command the match simulation consumes: both sides' effective strength plus the
    /// stakes. Command in → outcome out (ARCHITECTURE §8) — the sim returns a <see cref="MatchOutcome"/>
    /// the presentation replays, never shared mutable state read back.
    /// </summary>
    public readonly struct MatchCommand
    {
        public MatchCommand(TeamStrength home, TeamStrength away, MatchContext context)
        {
            Home = home;
            Away = away;
            Context = context;
        }

        public TeamStrength Home { get; }

        public TeamStrength Away { get; }

        public MatchContext Context { get; }
    }
}
