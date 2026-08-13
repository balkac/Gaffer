namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// One season of one career, in numbers: how many games and how many goals.
    ///
    /// <para><b>Why this had to exist.</b> The journey recorded MOMENTS and lifetime totals, and reading a
    /// career back out of it produced a flat stream of them — a list. The owner's reading of Gate B named
    /// the fault exactly: <em>"a career tells its seasons and its achievements."</em> A season is the unit
    /// a footballing life is told in, and it cannot be derived from the moments, because the years a player
    /// simply played well produce no moment at all and would read as gaps.</para>
    ///
    /// <para>A small struct held per season per followed player — a few dozen careers, a season each per
    /// year, so a fifty-season run holds a few thousand of these (PERFORMANCE §8).</para>
    /// </summary>
    public readonly struct PlayerSeason
    {
        public PlayerSeason(int season, int appearances, int goals)
        {
            Season = season;
            Appearances = appearances;
            Goals = goals;
        }

        public int Season { get; }

        public int Appearances { get; }

        public int Goals { get; }

        /// <summary>True when he did not play at all — a real thing to be able to say about a year.</summary>
        public bool WasSpentWatching => Appearances == 0;

        internal PlayerSeason Plus(int appearances, int goals)
        {
            return new PlayerSeason(Season, Appearances + appearances, Goals + goals);
        }
    }
}
