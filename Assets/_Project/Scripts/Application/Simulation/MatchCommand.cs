using Gaffer.Domain.Clubs;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The immutable command the match simulation consumes: both sides' effective strength, their squads
    /// (for named-scorer attribution), plus the stakes. Command in → outcome out (ARCHITECTURE §8) — the
    /// sim returns a <see cref="MatchOutcome"/> the presentation replays, never shared mutable state read
    /// back. The squads are optional: a strength-only harness or a restored match passes none, and goals
    /// stay side-attributed but unnamed.
    /// </summary>
    public readonly struct MatchCommand
    {
        public MatchCommand(TeamStrength home, TeamStrength away, MatchContext context)
            : this(home, away, null, null, context)
        {
        }

        public MatchCommand(TeamStrength home, TeamStrength away, Squad homeSquad, Squad awaySquad, MatchContext context)
        {
            Home = home;
            Away = away;
            HomeSquad = homeSquad;
            AwaySquad = awaySquad;
            Context = context;
        }

        public TeamStrength Home { get; }

        public TeamStrength Away { get; }

        public Squad HomeSquad { get; }

        public Squad AwaySquad { get; }

        public MatchContext Context { get; }
    }
}
