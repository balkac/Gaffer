using System.Collections.Generic;

namespace Gaffer.Application.Narrative
{
    /// <summary>One year of a career: what he played, and what is remembered of it.</summary>
    public sealed class ChronicleSeason
    {
        public ChronicleSeason(PlayerSeason played, IReadOnlyList<CareerMoment> moments)
        {
            Played = played;
            Moments = moments;
        }

        public PlayerSeason Played { get; }

        public int Season => Played.Season;

        /// <summary>What is remembered of it. Empty is common and correct — most years are just played.</summary>
        public IReadOnlyList<CareerMoment> Moments { get; }
    }
}
