using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

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

        public MatchCommand(TeamStrength home, TeamStrength away, IReadOnlyList<Player> homeEleven, IReadOnlyList<Player> awayEleven, MatchContext context)
            : this(home, away, homeEleven, awayEleven, ChanceProfile.Neutral, ChanceProfile.Neutral, context)
        {
        }

        public MatchCommand(TeamStrength home, TeamStrength away, IReadOnlyList<Player> homeEleven, IReadOnlyList<Player> awayEleven, ChanceProfile homeProfile, ChanceProfile awayProfile, MatchContext context)
        {
            Home = home;
            Away = away;
            HomeEleven = homeEleven;
            AwayEleven = awayEleven;
            HomeProfile = homeProfile;
            AwayProfile = awayProfile;
            Context = context;
        }

        public TeamStrength Home { get; }

        public TeamStrength Away { get; }

        /// <summary>
        /// The eleven actually on the pitch, which is who a goal may be credited to.
        ///
        /// <para>It used to be the whole SQUAD, and that was a real fault rather than a loose name: over
        /// half of a club's goals — measured at 51.5% across three seasons — were being credited to
        /// players the manager had left out. The scoreline named a substitute who never came on, and the
        /// narrative dropped the goal entirely, because it credits appearances from the eleven and found
        /// no eleven to hang it on. Picking a team meant nothing to who scored (PROGRESS 2026-08-13).</para>
        ///
        /// <para>Null for a strength-only match (the harness, a restored fixture): then nobody is named,
        /// which is correct — there is nobody there to name.</para>
        /// </summary>
        public IReadOnlyList<Player> HomeEleven { get; }

        public IReadOnlyList<Player> AwayEleven { get; }

        public ChanceProfile HomeProfile { get; }

        public ChanceProfile AwayProfile { get; }

        public MatchContext Context { get; }
    }
}
