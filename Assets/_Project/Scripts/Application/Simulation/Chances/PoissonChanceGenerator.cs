using System;
using System.Collections.Generic;
using Gaffer.Common;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Generates chances as a Poisson process: each side's expected count grows with its attack over
    /// the opponent's defence and its share of possession (from the midfield ratio), with a home-
    /// advantage lift (TDD §6 step 2). Chance quality varies around the tuned mean. Deterministic —
    /// every draw comes from the injected <see cref="IRandom"/>.
    /// </summary>
    public sealed class PoissonChanceGenerator : IChanceGenerator
    {
        private const int MinuteFirst = 1;
        private const int MinuteAfterLast = 91;

        private readonly MatchSimulationSettings _settings;

        // Scratch buffer reused across matches (cleared per call) so the per-match path allocates
        // nothing (PERFORMANCE §8). The returned list is valid until the next GenerateChances call —
        // the simulator consumes it synchronously before simulating the next match.
        private readonly List<Chance> _chances = new List<Chance>(32);

        public PoissonChanceGenerator(MatchSimulationSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<Chance> GenerateChances(MatchCommand command, IRandom rng)
        {
            List<Chance> chances = _chances;
            chances.Clear();
            double homePossession = ComputePossession(command.Home.Midfield, command.Away.Midfield);

            AppendSideChances(chances, TeamSide.Home, command.Home.Attack, command.Away.Defence, homePossession, _settings.HomeAdvantage, command.HomeProfile, rng);
            AppendSideChances(chances, TeamSide.Away, command.Away.Attack, command.Home.Defence, 1.0 - homePossession, 1.0, command.AwayProfile, rng);

            return chances;
        }

        private void AppendSideChances(List<Chance> chances, TeamSide side, double attack, double opponentDefence, double possession, double advantage, ChanceProfile profile, IRandom rng)
        {
            double strengthRatio = opponentDefence <= 0.0 ? _settings.MaxStrengthRatio : ClampRatio(attack / opponentDefence);
            double expectedChances = _settings.BaseChancesPerTeam * (2.0 * possession) * strengthRatio * advantage * profile.Volume;

            int count = SamplePoisson(expectedChances, rng);
            for (int i = 0; i < count; i++)
            {
                int minute = rng.NextInt(MinuteFirst, MinuteAfterLast);
                double quality = ComputeChanceQuality(profile.Quality, rng);
                chances.Add(new Chance(side, minute, quality));
            }
        }

        private static double ComputePossession(double homeMidfield, double awayMidfield)
        {
            double total = homeMidfield + awayMidfield;
            if (total <= 0.0)
            {
                return 0.5;
            }

            return homeMidfield / total;
        }

        private double ComputeChanceQuality(double qualityMultiplier, IRandom rng)
        {
            // Vary quality around the tuned mean (±variance), scaled by the tactical profile (the counter
            // sharpens it), capped so no chance is a certainty.
            double variance = _settings.ChanceQualityVariance;
            double spread = (1.0 - variance) + (rng.NextDouble() * 2.0 * variance);
            double quality = _settings.MeanChanceQuality * qualityMultiplier * spread;
            return Math.Min(quality, _settings.MaxChanceQuality);
        }

        private double ClampRatio(double ratio)
        {
            double floor = 1.0 / _settings.MaxStrengthRatio;
            if (ratio < floor)
            {
                return floor;
            }

            if (ratio > _settings.MaxStrengthRatio)
            {
                return _settings.MaxStrengthRatio;
            }

            return ratio;
        }

        private static int SamplePoisson(double lambda, IRandom rng)
        {
            // Knuth's algorithm: multiply uniform draws until the product drops below e^-lambda.
            double limit = Math.Exp(-lambda);
            int count = 0;
            double product = 1.0;
            do
            {
                count++;
                product *= rng.NextDouble();
            }
            while (product > limit);

            return count - 1;
        }
    }
}
