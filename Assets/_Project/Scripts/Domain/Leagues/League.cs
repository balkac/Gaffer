using System.Collections.Generic;
using Gaffer.Domain.Clubs;

namespace Gaffer.Domain.Leagues
{
    /// <summary>A league: a (fictional, generated) name and the clubs that contest it for one season.</summary>
    public sealed class League
    {
        public League(string name, IReadOnlyList<Club> clubs)
        {
            Name = name;
            Clubs = clubs;
        }

        public string Name { get; }

        public IReadOnlyList<Club> Clubs { get; }
    }
}
