using System.Collections.Generic;

namespace Gaffer.Application.Season
{
    /// <summary>The outcome of advancing one match week: the round played and its results.</summary>
    public sealed class WeekResult
    {
        public WeekResult(int round, IReadOnlyList<MatchResult> matches)
        {
            Round = round;
            Matches = matches;
        }

        public int Round { get; }

        public IReadOnlyList<MatchResult> Matches { get; }
    }
}
