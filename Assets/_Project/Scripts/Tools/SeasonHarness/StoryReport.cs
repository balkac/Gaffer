using System.Collections.Generic;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>
    /// What <see cref="StoryProbe"/> found: every career it followed, strongest arc first, plus the two
    /// numbers that say whether there was anything to find at all.
    ///
    /// <para><see cref="MomentCount"/> against <see cref="Careers"/> is the honest health check, and it
    /// fails in both directions. Near zero and the world produced no stories; enormous and every week
    /// produced one, which is the same failure wearing the opposite face — nothing is rare when
    /// everything is a moment.</para>
    /// </summary>
    public sealed class StoryReport
    {
        public StoryReport(string club, int seasons, int momentCount, IReadOnlyList<StoryArc> arcs)
        {
            Club = club;
            Seasons = seasons;
            MomentCount = momentCount;
            Arcs = arcs;
        }

        public string Club { get; }

        public int Seasons { get; }

        /// <summary>Every moment recognised across every career followed.</summary>
        public int MomentCount { get; }

        public int Careers => Arcs.Count;

        public double MomentsPerCareer => Careers == 0 ? 0.0 : (double)MomentCount / Careers;

        /// <summary>The careers, strongest arc first.</summary>
        public IReadOnlyList<StoryArc> Arcs { get; }
    }
}
