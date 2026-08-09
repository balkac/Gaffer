using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>One generation rank's strength across every sampled league.</summary>
    public sealed class GeneratorRankRow
    {
        public GeneratorRankRow(int rank, double meanStrength, double minStrength, double maxStrength, int timesStrongest)
        {
            Rank = rank;
            MeanStrength = meanStrength;
            MinStrength = minStrength;
            MaxStrength = maxStrength;
            TimesStrongest = timesStrongest;
        }

        public int Rank { get; }

        public double MeanStrength { get; }

        public double MinStrength { get; }

        public double MaxStrength { get; }

        /// <summary>Leagues in which the club at this rank turned out to be the strongest in the league.</summary>
        public int TimesStrongest { get; }
    }

    public sealed class GeneratorRankReport
    {
        public GeneratorRankReport(int leagues, int clubCount, IReadOnlyList<GeneratorRankRow> rows, double meanInversions, int rankOneStrongest)
        {
            Leagues = leagues;
            ClubCount = clubCount;
            Rows = rows;
            MeanInversions = meanInversions;
            RankOneStrongest = rankOneStrongest;
        }

        public int Leagues { get; }

        public int ClubCount { get; }

        public IReadOnlyList<GeneratorRankRow> Rows { get; }

        /// <summary>Adjacent rank pairs per league that come out in the wrong strength order.</summary>
        public double MeanInversions { get; }

        /// <summary>Leagues in which rank 1 really was the strongest club.</summary>
        public int RankOneStrongest { get; }
    }

    /// <summary>
    /// Measures whether <see cref="LeagueGenerator"/>'s rank actually orders the clubs by strength — the
    /// question "does rank 1 genuinely get the strongest squad" asked of the SHIPPED generator rather than
    /// of the harness's own quality curve.
    /// <para>
    /// It is a band-overlap measurement. The generator walks a centre from <c>TopCentre</c> to
    /// <c>BottomCentre</c> and draws each club's roster from a fixed half-width band around it, so the step
    /// between neighbouring ranks is small next to the spread inside one band, and which of two neighbours
    /// ends up stronger is partly the draw. This puts a number on how often that happens.
    /// </para>
    /// <para>Raw English throughout: never-shipped developer tooling, the NON-NEGOTIABLE #8 exemption.</para>
    /// </summary>
    public sealed class GeneratorRankProbe
    {
        public GeneratorRankReport Measure(int leagues, int clubCount, ulong seed)
        {
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator()));

            var totals = new double[clubCount];
            var minimums = new double[clubCount];
            var maximums = new double[clubCount];
            var strongest = new int[clubCount];
            for (int rank = 0; rank < clubCount; rank++)
            {
                minimums[rank] = double.MaxValue;
                maximums[rank] = double.MinValue;
            }

            long inversions = 0;
            for (int league = 0; league < leagues; league++)
            {
                // One stream per league, mixed from the seed the same way the run's world generation is,
                // so a sample reproduces exactly (NON-NEGOTIABLE #2).
                League generated = generator.Generate(
                    clubCount,
                    new SplitMix64RandomNumberGenerator(seed + ((ulong)league * 0x9E3779B97F4A7C15UL)));

                int best = 0;
                double bestStrength = double.MinValue;
                for (int rank = 0; rank < generated.Clubs.Count; rank++)
                {
                    double strength = Overall(generated.Clubs[rank].Strength);
                    totals[rank] += strength;
                    if (strength < minimums[rank])
                    {
                        minimums[rank] = strength;
                    }

                    if (strength > maximums[rank])
                    {
                        maximums[rank] = strength;
                    }

                    if (strength > bestStrength)
                    {
                        bestStrength = strength;
                        best = rank;
                    }

                    if (rank > 0 && strength > Overall(generated.Clubs[rank - 1].Strength))
                    {
                        inversions++;
                    }
                }

                strongest[best]++;
            }

            var rows = new List<GeneratorRankRow>(clubCount);
            for (int rank = 0; rank < clubCount; rank++)
            {
                rows.Add(new GeneratorRankRow(
                    rank,
                    leagues == 0 ? 0.0 : totals[rank] / leagues,
                    minimums[rank] == double.MaxValue ? 0.0 : minimums[rank],
                    maximums[rank] == double.MinValue ? 0.0 : maximums[rank],
                    strongest[rank]));
            }

            return new GeneratorRankReport(
                leagues,
                clubCount,
                rows,
                leagues == 0 ? 0.0 : (double)inversions / leagues,
                strongest.Length == 0 ? 0 : strongest[0]);
        }

        private static double Overall(TeamStrength strength)
        {
            return (strength.Attack + strength.Midfield + strength.Defence) / 3.0;
        }
    }
}
