using System;
using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>Builds a league of teams with a believable quality spread, deterministically from the seed.</summary>
    public sealed class LeagueFactory
    {
        // PERFORMANCE §8: the allocation-free sort overload is Sort(Comparison<T>) with a *cached*
        // delegate, and C# 9 caches no method group.
        private static readonly Comparison<TeamProfile> ByStrength = CompareByStrength;

        private readonly ClubNameGenerator _names = new ClubNameGenerator();

        /// <summary>
        /// The quality curve sets each team's centre, per-axis jitter spreads it, and THEN the pre-season
        /// rank is assigned from the strength that came out.
        /// <para>
        /// The ordering is the load-bearing part. <see cref="HarnessConfig.AxisJitter"/> (±3 per axis) is
        /// larger than one step of the quality curve (24 points over 19 gaps, so ~1.26), so ranking by the
        /// curve alone hands rank #2 a stronger squad than rank #1 in roughly one league in five. That is
        /// not noise the harness averages away: <c>CreateTeams</c> is called ONCE and the same strengths
        /// are replayed for every season of the run, so a single unlucky draw becomes a permanent 1000-season
        /// verdict — the "#2 wins 65% more titles than #1" the report used to show. Ranking after the jitter
        /// costs nothing and makes "titles by pre-season rank" mean what it says.
        /// </para>
        /// <para>
        /// <see cref="TeamProfile.BaseQuality"/> is the realised strength for the same reason: it is what
        /// <see cref="HarnessStatistics.RecordMatch"/> decides the favourite from, and the curve value would
        /// have called the weaker side the favourite in exactly the fixtures the jitter had flipped.
        /// </para>
        /// </summary>
        public IReadOnlyList<TeamProfile> CreateTeams(HarnessConfig config, IRandom rng)
        {
            IReadOnlyList<string> names = _names.GenerateDistinct(config.TeamCount, rng);

            var drawn = new List<TeamProfile>(config.TeamCount);
            for (int i = 0; i < config.TeamCount; i++)
            {
                double position = config.TeamCount == 1 ? 0.0 : (double)i / (config.TeamCount - 1);
                double centre = config.TopQuality + (config.BottomQuality - config.TopQuality) * position;

                double attack = Jitter(centre, config.AxisJitter, rng);
                double midfield = Jitter(centre, config.AxisJitter, rng);
                double defence = Jitter(centre, config.AxisJitter, rng);

                var strength = new TeamStrength(attack, midfield, defence);
                drawn.Add(new TeamProfile(i, names[i], Overall(strength), strength));
            }

            drawn.Sort(ByStrength);

            var ranked = new List<TeamProfile>(drawn.Count);
            for (int rank = 0; rank < drawn.Count; rank++)
            {
                TeamProfile team = drawn[rank];
                ranked.Add(new TeamProfile(rank, team.Name, team.BaseQuality, team.Strength));
            }

            return ranked;
        }

        private static double Jitter(double centre, double amplitude, IRandom rng)
        {
            double offset = (rng.NextDouble() * 2.0 - 1.0) * amplitude;
            return centre + offset;
        }

        // The three axes weigh equally in the chance model, so their mean is the fair scalar summary.
        private static double Overall(TeamStrength strength)
        {
            return (strength.Attack + strength.Midfield + strength.Defence) / 3.0;
        }

        // Strongest first. Two teams on identical strength fall back to the draw order, so the ranking is
        // total and the same seed reproduces the same league (NON-NEGOTIABLE #2) — List.Sort is unstable.
        private static int CompareByStrength(TeamProfile left, TeamProfile right)
        {
            int byStrength = right.BaseQuality.CompareTo(left.BaseQuality);
            return byStrength != 0 ? byStrength : left.Rank.CompareTo(right.Rank);
        }
    }
}
