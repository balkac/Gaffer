using System.Collections.Generic;
using Gaffer.Application.Narrative;
using Gaffer.Domain.Players;

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
        private readonly JourneyLog _log;

        public StoryReport(string club, int seasons, int momentCount, IReadOnlyList<StoryArc> arcs, JourneyLog log = null)
        {
            Club = club;
            Seasons = seasons;
            MomentCount = momentCount;
            Arcs = arcs;
            _log = log;
        }

        /// <summary>A career told season by season — what the probe prints, since a flat stream of
        /// moments reads as a list rather than a career.</summary>
        public CareerChronicle ChronicleOf(PlayerId player)
        {
            return CareerChronicle.Create(_log != null ? _log.Find(player) : null);
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
