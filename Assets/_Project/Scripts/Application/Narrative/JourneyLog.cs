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
        // Two views of the same journeys, and both are needed. The dictionary answers "his history" in
        // one step — keyed by the raw int rather than PlayerId, since an enum-or-struct key drags a
        // comparer through IL2CPP (PERFORMANCE §8). The list is what everything ITERATES.
        //
        // The list is not a convenience. Dictionary enumeration order is not part of its contract, and
        // three things walk these journeys in order: the save writes them, the season recap sorts them by
        // week and leaves ties in whatever order it found them, and the Gate B probe ranks them. Leaning
        // on the dictionary would make a save's byte order and a recap's tie order incidental — the shape
        // of bug that reproduces on one machine and not another. Exposing IEnumerable also boxed the
        // struct enumerator at every one of those call sites.
        private readonly Dictionary<int, PlayerJourney> _byPlayer = new Dictionary<int, PlayerJourney>();
        private readonly List<PlayerJourney> _journeys = new List<PlayerJourney>();

        public int Count => _journeys.Count;

        /// <summary>Every journey, in the order they were opened — stable, and indexable without boxing.</summary>
        public IReadOnlyList<PlayerJourney> Journeys => _journeys;

        /// <summary>His journey, or null when the manager has never had anything to do with him.</summary>
        public PlayerJourney Find(PlayerId player)
        {
            return _byPlayer.TryGetValue(player.Value, out PlayerJourney journey) ? journey : null;
        }

        public bool IsFollowing(PlayerId player)
        {
            return _byPlayer.ContainsKey(player.Value);
        }

        /// <summary>
        /// His journey, opening one if this is the first the log has heard of him. The single door in:
        /// "when does a player start being followed" is one rule in one place rather than a decision each
        /// caller makes for itself (ARCHITECTURE §8a).
        /// </summary>
        public PlayerJourney Follow(PlayerId player, string name)
        {
            if (_byPlayer.TryGetValue(player.Value, out PlayerJourney existing))
            {
                return existing;
            }

            var opened = new PlayerJourney(player, name);
            _byPlayer.Add(player.Value, opened);
            _journeys.Add(opened);
            return opened;
        }

        /// <summary>
        /// Puts a journey back as a save wrote it. Replaces in place when one is already there, so a
        /// restore cannot leave the list holding a stale copy the dictionary has already forgotten.
        /// </summary>
        public void Restore(PlayerJourney journey)
        {
            if (_byPlayer.TryGetValue(journey.Player.Value, out PlayerJourney existing))
            {
                _journeys[_journeys.IndexOf(existing)] = journey;
            }
            else
            {
                _journeys.Add(journey);
            }

            _byPlayer[journey.Player.Value] = journey;
        }
    }
}
