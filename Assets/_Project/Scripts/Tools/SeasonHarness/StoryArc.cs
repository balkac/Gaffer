using System.Collections.Generic;
using Gaffer.Application.Narrative;
using Gaffer.Domain.Players;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>
    /// One player's career as the Gate B instrument reads it: his moments, in order, and a score for how
    /// much of an ARC they add up to.
    ///
    /// <para><b>The score is the instrument, not the game.</b> Nothing in the shipped run scores a career,
    /// and nothing should — this exists so "did a story emerge" can be answered by running a command
    /// instead of by playing five seasons and squinting. When Gate B is settled the apparatus goes and the
    /// numbers stay (PERFORMANCE §11).</para>
    /// </summary>
    public sealed class StoryArc
    {
        public StoryArc(PlayerId player, string name, IReadOnlyList<CareerMoment> moments, double score, int firstSeason, int lastSeason)
        {
            Player = player;
            Name = name;
            Moments = moments;
            Score = score;
            FirstSeason = firstSeason;
            LastSeason = lastSeason;
        }

        public PlayerId Player { get; }

        public string Name { get; }

        public IReadOnlyList<CareerMoment> Moments { get; }

        /// <summary>How strong an arc this is, by <see cref="StoryArcScoring"/>. Comparable, not meaningful.</summary>
        public double Score { get; }

        public int FirstSeason { get; }

        public int LastSeason { get; }

        public int SeasonsSpanned => (LastSeason - FirstSeason) + 1;
    }
}
