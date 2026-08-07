using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// The managed club's team sheet after a lineup, formation or tactics command — everything a view
    /// draws about the eleven, already derived. The view used to reach into the core for each of these:
    /// walking the season's squad to work out who was benched, and building a second
    /// <see cref="EffectiveStrengthBuilder"/> (on the <em>default</em> trait catalog, so its ATK/MID/DEF
    /// pills disagreed with the sim) to show the side's strength. Command in, outcome out
    /// (ARCHITECTURE §8): the eleven is chosen once, in the core, and the view replays this.
    /// </summary>
    public sealed class LineupOutcome
    {
        public Formation Formation { get; init; }

        public Tactics Tactics { get; init; }

        /// <summary>
        /// One entry per formation slot, index-aligned with <see cref="Simulation.Formation.Slots"/>;
        /// <c>null</c> where the slot is empty. A snapshot — later commands do not change it.
        /// </summary>
        public IReadOnlyList<Player> Slots { get; init; }

        /// <summary>The players actually fielded, in slot order (the non-empty <see cref="Slots"/>).</summary>
        public IReadOnlyList<Player> Starters { get; init; }

        /// <summary>Everyone in the squad who is not in a slot, in squad order.</summary>
        public IReadOnlyList<Player> Bench { get; init; }

        /// <summary>True when every slot is filled — the eleven the manager will actually field.</summary>
        public bool IsComplete { get; init; }

        /// <summary>
        /// The strength this eleven and these tactics derive to, through the run's own trait catalog and
        /// tactics balance — the same derivation the match uses, so the pills cannot disagree with the sim.
        /// </summary>
        public TeamStrength Strength { get; init; }

        /// <summary>
        /// How these tactics shape the side's chances (volume/quality vs balanced), through the run's
        /// tactics balance.
        /// </summary>
        public ChanceProfile ChanceProfile { get; init; }
    }
}
