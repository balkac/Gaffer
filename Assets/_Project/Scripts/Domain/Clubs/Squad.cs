using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Domain.Clubs
{
    /// <summary>
    /// A club's roster of players — the source a match's <see cref="TeamStrength"/> is derived from
    /// (BuildEffectiveStrength). Immutable: it copies the players in so no caller can mutate the roster
    /// behind the club's back. For now it is a flat list the strength builder groups by position;
    /// selection (starting XI, formation) and tactics layer on later (TDD §6.1).
    /// </summary>
    public sealed class Squad
    {
        private readonly List<Player> _players;

        public Squad(IReadOnlyList<Player> players)
        {
            _players = new List<Player>(players);
        }

        public IReadOnlyList<Player> Players => _players;

        public int Count => _players.Count;
    }
}
