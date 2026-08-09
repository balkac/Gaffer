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
        /// <summary>
        /// Every value is required: this record is only ever built by <see cref="RunSession"/> when it
        /// binds the eleven, where all of it is known, so there is no default worth having and an omitted
        /// field should not compile.
        /// </summary>
        public LineupOutcome(
            Formation formation,
            Tactics tactics,
            IReadOnlyList<Player> slots,
            IReadOnlyList<Player> starters,
            IReadOnlyList<Player> bench,
            bool isComplete,
            TeamStrength strength,
            ChanceProfile chanceProfile)
        {
            Formation = formation;
            Tactics = tactics;
            Slots = slots;
            Starters = starters;
            Bench = bench;
            IsComplete = isComplete;
            Strength = strength;
            ChanceProfile = chanceProfile;
        }

        public Formation Formation { get; }

        public Tactics Tactics { get; }

        /// <summary>
        /// One entry per formation slot, index-aligned with <see cref="Simulation.Formation.Slots"/>;
        /// <c>null</c> where the slot is empty. A snapshot — later commands do not change it.
        /// </summary>
        public IReadOnlyList<Player> Slots { get; }

        /// <summary>The players actually fielded, in slot order (the non-empty <see cref="Slots"/>).</summary>
        public IReadOnlyList<Player> Starters { get; }

        /// <summary>Everyone in the squad who is not in a slot, in squad order.</summary>
        public IReadOnlyList<Player> Bench { get; }

        /// <summary>True when every slot is filled — the eleven the manager will actually field.</summary>
        public bool IsComplete { get; }

        /// <summary>
        /// The strength this eleven and these tactics derive to, through the run's own trait catalog and
        /// tactics balance — the same derivation the match uses, so the pills cannot disagree with the sim.
        /// </summary>
        public TeamStrength Strength { get; }

        /// <summary>
        /// How these tactics shape the side's chances (volume/quality vs balanced), through the run's
        /// tactics balance.
        /// </summary>
        public ChanceProfile ChanceProfile { get; }
    }
}
