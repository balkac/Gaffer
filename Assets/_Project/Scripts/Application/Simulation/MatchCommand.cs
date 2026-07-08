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
            : this(home, away, null, null, ChanceProfile.Neutral, ChanceProfile.Neutral, context)
        {
        }

        public MatchCommand(TeamStrength home, TeamStrength away, Squad homeSquad, Squad awaySquad, MatchContext context)
            : this(home, away, homeSquad, awaySquad, ChanceProfile.Neutral, ChanceProfile.Neutral, context)
        {
        }

        public MatchCommand(TeamStrength home, TeamStrength away, Squad homeSquad, Squad awaySquad, ChanceProfile homeProfile, ChanceProfile awayProfile, MatchContext context)
        {
            Home = home;
            Away = away;
            HomeSquad = homeSquad;
            AwaySquad = awaySquad;
            HomeProfile = homeProfile;
            AwayProfile = awayProfile;
            Context = context;
        }

        public TeamStrength Home { get; }

        public TeamStrength Away { get; }

        public Squad HomeSquad { get; }

        public Squad AwaySquad { get; }

        public ChanceProfile HomeProfile { get; }

        public ChanceProfile AwayProfile { get; }

        public MatchContext Context { get; }
    }
}
