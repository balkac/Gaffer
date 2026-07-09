using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Domain.Clubs;
using Gaffer.Application.Simulation;
using Gaffer.Common;

namespace Gaffer.Editor.Harness
{
    /// <summary>Builds a league of teams with a believable quality spread, deterministically from the seed.</summary>
    public sealed class LeagueFactory
    {
        private readonly ClubNameGenerator _names = new ClubNameGenerator();

        public IReadOnlyList<TeamProfile> CreateTeams(HarnessConfig config, IRandom rng)
        {
            IReadOnlyList<string> names = _names.GenerateDistinct(config.TeamCount, rng);

            var teams = new List<TeamProfile>(config.TeamCount);
            for (int rank = 0; rank < config.TeamCount; rank++)
            {
                double position = config.TeamCount == 1 ? 0.0 : (double)rank / (config.TeamCount - 1);
                double baseQuality = config.TopQuality + (config.BottomQuality - config.TopQuality) * position;

                double attack = Jitter(baseQuality, config.AxisJitter, rng);
                double midfield = Jitter(baseQuality, config.AxisJitter, rng);
                double defence = Jitter(baseQuality, config.AxisJitter, rng);

                teams.Add(new TeamProfile(rank, names[rank], baseQuality, new TeamStrength(attack, midfield, defence)));
            }

            return teams;
        }

        private static double Jitter(double centre, double amplitude, IRandom rng)
        {
            double offset = (rng.NextDouble() * 2.0 - 1.0) * amplitude;
            return centre + offset;
        }
    }
}
