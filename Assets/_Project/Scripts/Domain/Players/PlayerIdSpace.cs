namespace Gaffer.Domain.Players
{
    /// <summary>
    /// How player ids are partitioned, stated once so the two allocators cannot drift into each other.
    ///
    /// <para><b>Why this exists.</b> A run allocates ids from two independent places: league squads hand
    /// out ids derived from the league's own highest, and the transfer market hands out ids from a base
    /// well above it. Those two were only kept apart by arithmetic that happened to work — the market's
    /// per-season stride outran the league's few academy arrivals a year — and nothing said so. Once the
    /// market became persistent (players are no longer thrown away every summer, PROGRESS 2026-08-13) a
    /// collision stopped being theoretical: a signed market player carries his market id into a league
    /// squad, so the league's "one past the highest id here" would start allocating inside market space.
    /// Two players sharing an id would merge two careers in the journey log — the thing Faz 5 is built
    /// on — so the partition is written down and pinned by a test rather than left to be re-derived.</para>
    ///
    /// <para><b>The rule.</b> Ids below <see cref="MarketBase"/> are league-native (generation and academy
    /// intake); ids at or above it belong to the market, sliced further by <see cref="SeasonStride"/> so
    /// each season's arrivals get a fresh block and <b>no id is ever reused</b>, including one vacated by
    /// a player who has retired out of the pool.</para>
    /// </summary>
    public static class PlayerIdSpace
    {
        /// <summary>The first id belonging to the market. Everything below is league-native.</summary>
        public const int MarketBase = 1_000_000;

        /// <summary>
        /// How far apart successive seasons' market blocks sit. It is also the ceiling on how many players
        /// one season may bring into the market — the pool at generation, or a season's intake — because
        /// the block after it belongs to the next season.
        /// </summary>
        public const int SeasonStride = 100_000;

        /// <summary>True when this id was handed out by the market rather than by a league squad.</summary>
        public static bool IsMarket(PlayerId id)
        {
            return id.Value >= MarketBase;
        }

        /// <summary>
        /// The first id of a season's market block. Clamped so a run long enough to overflow the block
        /// arithmetic keeps returning ids inside the market space instead of wrapping negative and
        /// colliding with league-native ones (CONVENTIONS §6). The 1000-season harness sits far inside
        /// the clamp; nothing shipped approaches it.
        /// </summary>
        public static int SeasonBase(int seasonNumber)
        {
            int season = seasonNumber < 0 ? 0 : seasonNumber;
            int maxSeason = (int.MaxValue - MarketBase) / SeasonStride;
            return MarketBase + ((season > maxSeason ? maxSeason : season) * SeasonStride);
        }
    }
}
