using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// A shape for the starting eleven as eleven specific-role slots (TDD §6.1) — a keeper plus ten
    /// outfield roles, so a 4-3-3 asks for wingers where a 4-4-2 asks for wide midfielders. The lineup
    /// selector fills each slot with the best player for that role. A small preset list covers the common
    /// shapes; a data-driven set can replace it later without touching call sites.
    /// </summary>
    public readonly struct Formation
    {
        public Formation(string name, IReadOnlyList<PlayerRole> slots)
        {
            Name = name;
            Slots = slots;
        }

        public string Name { get; }

        public IReadOnlyList<PlayerRole> Slots { get; }

        /// <summary>Eleven for every valid formation.</summary>
        public int Total => Slots.Count;

        public static Formation F442 => new Formation("4-4-2", new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
            PlayerRole.RightMidfield, PlayerRole.CentralMidfield, PlayerRole.CentralMidfield, PlayerRole.LeftMidfield,
            PlayerRole.Striker, PlayerRole.Striker,
        });

        public static Formation F433 => new Formation("4-3-3", new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
            PlayerRole.CentralMidfield, PlayerRole.CentralMidfield, PlayerRole.AttackingMidfield,
            PlayerRole.RightWing, PlayerRole.Striker, PlayerRole.LeftWing,
        });

        public static Formation F451 => new Formation("4-5-1", new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
            PlayerRole.RightMidfield, PlayerRole.CentralMidfield, PlayerRole.CentralMidfield, PlayerRole.LeftMidfield,
            PlayerRole.AttackingMidfield, PlayerRole.Striker,
        });

        public static Formation F352 => new Formation("3-5-2", new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.CentreBack,
            PlayerRole.DefensiveMidfield, PlayerRole.RightMidfield, PlayerRole.CentralMidfield, PlayerRole.LeftMidfield, PlayerRole.AttackingMidfield,
            PlayerRole.Striker, PlayerRole.Striker,
        });

        public static Formation F532 => new Formation("5-3-2", new[]
        {
            PlayerRole.Goalkeeper,
            PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
            PlayerRole.DefensiveMidfield, PlayerRole.CentralMidfield, PlayerRole.AttackingMidfield,
            PlayerRole.Striker, PlayerRole.Striker,
        });

        public static IReadOnlyList<Formation> Presets => new[] { F442, F433, F352, F532, F451 };
    }
}
