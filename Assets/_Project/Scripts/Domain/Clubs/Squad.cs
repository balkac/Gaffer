using System.Collections.Generic;
using Gaffer.Domain.Players;

namespace Gaffer.Domain.Clubs
{
    /// <summary>
    /// A club's roster of players — the source a match's <see cref="TeamStrength"/> is derived from
    /// (BuildEffectiveStrength). Immutable: the public constructor copies the players in so no caller can
    /// mutate the roster behind the club's back, and <see cref="Owning"/> is the opt-in no-copy path for a
    /// caller handing over a list it just built and will not keep. For now it is a flat list the strength
    /// builder groups by position;
    /// selection (starting XI, formation) and tactics layer on later (TDD §6.1).
    /// </summary>
    public sealed class Squad
    {
        private readonly List<Player> _players;

        /// <summary>Builds a squad from any roster; the players are copied in, so a caller keeping its
        /// own reference to the list cannot mutate the roster behind the club's back.</summary>
        public Squad(IReadOnlyList<Player> players)
        {
            _players = new List<Player>(players);
        }

        // Takes ownership of a list the caller built and hands over — no copy, so the roster must have
        // no other live reference. Private, with <see cref="Owning"/> as the one deliberate door onto
        // it, so the precondition is stated in exactly one place instead of being implied by an
        // overload. Add/Remove used to pay for two lists each (build one, then copy it in the public
        // constructor) — four heap objects to append a single player, on the path a transfer window
        // walks per signing (PERFORMANCE §8). Note that a `new Squad(someList)` *inside* this class
        // binds here, not to the copying constructor: only hand it lists built right here.
        private Squad(List<Player> owned)
        {
            _players = owned;
        }

        /// <summary>
        /// Builds a squad that <em>takes over</em> <paramref name="players"/> instead of copying it —
        /// for a caller that has just built the list and hands it over, keeping no reference of its own
        /// (the squad generator, the season rollover, squad renewal). Two entry points, two audiences:
        /// the constructor is for untrusted or shared input and pays a copy for that safety; this is for
        /// a list whose only owner is about to be the squad, and skips it. Squad stays immutable from
        /// the outside either way — nothing here ever hands <c>_players</c> back out as a mutable list.
        /// <para><strong>The precondition is the caller's to keep:</strong> pass a list you built and
        /// will not touch again. Hold on to it and you can mutate a club's roster behind its back —
        /// use the constructor instead whenever you are not certain.</para>
        /// </summary>
        public static Squad Owning(List<Player> players)
        {
            return new Squad(players);
        }

        public IReadOnlyList<Player> Players => _players;

        public int Count => _players.Count;

        public bool Contains(PlayerId id)
        {
            foreach (Player player in _players)
            {
                if (player.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns a new squad with the player added; the original is untouched (immutable).</summary>
        public Squad Add(Player player)
        {
            // Pre-sized to the exact final count: the collection-initializer form
            // (`new List<Player>(_players) { player }`) sizes to Count and then resizes on the append.
            var next = new List<Player>(_players.Count + 1);
            next.AddRange(_players);
            next.Add(player);
            return new Squad(next);
        }

        /// <summary>Returns a new squad without the identified player; unchanged if he is not on the roster.</summary>
        public Squad Remove(PlayerId id)
        {
            var next = new List<Player>(_players.Count);
            foreach (Player player in _players)
            {
                if (player.Id != id)
                {
                    next.Add(player);
                }
            }

            return new Squad(next);
        }
    }
}
