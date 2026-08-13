namespace Gaffer.Application.Season
{
    /// <summary>
    /// When a league fixture stops being just another game — the balance <see cref="MatchContextBuilder"/>
    /// reads (NON-NEGOTIABLE #3). Immutable, all-optional constructor, cached <see cref="Default"/>: the
    /// convention every settings type in the core follows.
    /// </summary>
    public sealed class MatchContextSettings
    {
        public MatchContextSettings(
            int runInRounds = 8,
            int titleContenders = 3,
            int relegationPlaces = 3)
        {
            RunInRounds = runInRounds < 0 ? 0 : runInRounds;
            TitleContenders = titleContenders < 0 ? 0 : titleContenders;
            RelegationPlaces = relegationPlaces < 0 ? 0 : relegationPlaces;
        }

        /// <summary>
        /// How many rounds of "run-in" a season ends with — the stretch where the table has stopped being
        /// a rumour and a result between two contenders decides something. Before it, position is noise:
        /// two clubs top of the table in September are not playing a title decider, they are playing in
        /// September.
        /// </summary>
        public int RunInRounds { get; }

        /// <summary>How many places count as being in the title race.</summary>
        public int TitleContenders { get; }

        /// <summary>How many places from the bottom count as being in the relegation fight.</summary>
        public int RelegationPlaces { get; }

        public static MatchContextSettings Default { get; } = new MatchContextSettings();
    }
}
