using System.Collections.Generic;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// A shape for the starting eleven — how many defenders, midfielders, and forwards line up in front of
    /// the one keeper (TDD §6.1). The lineup selector fills each line to these counts. A small preset list
    /// covers the common shapes; a data-driven set can replace it later without touching call sites.
    /// </summary>
    public readonly struct Formation
    {
        public Formation(string name, int defenders, int midfielders, int forwards)
        {
            Name = name;
            Defenders = defenders;
            Midfielders = midfielders;
            Forwards = forwards;
        }

        public string Name { get; }

        public int Defenders { get; }

        public int Midfielders { get; }

        public int Forwards { get; }

        /// <summary>Outfield players plus the one keeper — eleven for every valid formation.</summary>
        public int Total => 1 + Defenders + Midfielders + Forwards;

        public static Formation F442 => new Formation("4-4-2", 4, 4, 2);

        public static Formation F433 => new Formation("4-3-3", 4, 3, 3);

        public static Formation F352 => new Formation("3-5-2", 3, 5, 2);

        public static Formation F532 => new Formation("5-3-2", 5, 3, 2);

        public static Formation F451 => new Formation("4-5-1", 4, 5, 1);

        public static IReadOnlyList<Formation> Presets => new[] { F442, F433, F352, F532, F451 };
    }
}
