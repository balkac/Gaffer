using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// Every journey the run is keeping — the memory half of "Story = Simulation + Character + Memory"
    /// (CLAUDE.md). What Faz 5 is built on, and what the market had to become persistent for: a log
    /// cannot follow a player the world deletes every summer (PROGRESS 2026-08-13).
    ///
    /// <para><b>Scoped to players the manager has touched</b>, and that scope is a hard requirement, not
    /// a tidiness. The world holds 50,000 players; a journey each would be memory and save weight spent
    /// on careers nobody will ever read. A journey opens when a player joins the squad and stays open
    /// afterwards — including after he is sold, which is the owner's decision (2026-08-13) and the whole
    /// point of the Hall of Legends: the boy you let go is a better story three seasons later than the
    /// one you kept.</para>
    /// </summary>
    public sealed class JourneyLog
    {
        // Keyed by the raw int rather than PlayerId: the id set is sparse and unbounded, so a dictionary
        // is right, but an enum-or-struct key would drag a comparer through IL2CPP (PERFORMANCE §8).
        private readonly Dictionary<int, PlayerJourney> _journeys = new Dictionary<int, PlayerJourney>();

        public int Count => _journeys.Count;

        public IEnumerable<PlayerJourney> Journeys => _journeys.Values;

        /// <summary>His journey, or null when the manager has never had anything to do with him.</summary>
        public PlayerJourney Find(PlayerId player)
        {
            return _journeys.TryGetValue(player.Value, out PlayerJourney journey) ? journey : null;
        }

        public bool IsFollowing(PlayerId player)
        {
            return _journeys.ContainsKey(player.Value);
        }

        /// <summary>
        /// His journey, opening one if this is the first the log has heard of him. The single door in:
        /// "when does a player start being followed" is one rule in one place rather than a decision each
        /// caller makes for itself (ARCHITECTURE §8a).
        /// </summary>
        public PlayerJourney Follow(PlayerId player, string name)
        {
            if (_journeys.TryGetValue(player.Value, out PlayerJourney existing))
            {
                return existing;
            }

            var opened = new PlayerJourney(player, name);
            _journeys.Add(player.Value, opened);
            return opened;
        }

        public void Restore(PlayerJourney journey)
        {
            _journeys[journey.Player.Value] = journey;
        }
    }
}
