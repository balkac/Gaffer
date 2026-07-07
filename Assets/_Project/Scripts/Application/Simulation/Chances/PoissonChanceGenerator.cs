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
        private const double MaxChanceQuality = 0.95;

        private readonly MatchSimulationSettings _settings;

        public PoissonChanceGenerator(MatchSimulationSettings settings)
        {
            _settings = settings;
        }

        public IReadOnlyList<Chance> GenerateChances(MatchCommand command, IRandom rng)
        {
            var chances = new List<Chance>();
            double homePossession = ComputePossession(command.Home.Midfield, command.Away.Midfield);

            AppendSideChances(chances, TeamSide.Home, command.Home.Attack, command.Away.Defence, homePossession, _settings.HomeAdvantage, rng);
            AppendSideChances(chances, TeamSide.Away, command.Away.Attack, command.Home.Defence, 1.0 - homePossession, 1.0, rng);

            return chances;
        }

        private void AppendSideChances(List<Chance> chances, TeamSide side, double attack, double opponentDefence, double possession, double advantage, IRandom rng)
        {
            double strengthRatio = opponentDefence <= 0.0 ? _settings.MaxStrengthRatio : ClampRatio(attack / opponentDefence);
            double expectedChances = _settings.BaseChancesPerTeam * (2.0 * possession) * strengthRatio * advantage;

            int count = SamplePoisson(expectedChances, rng);
            for (int i = 0; i < count; i++)
            {
                int minute = rng.NextInt(MinuteFirst, MinuteAfterLast);
                double quality = ComputeChanceQuality(rng);
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

        private double ComputeChanceQuality(IRandom rng)
        {
            // Vary quality 0.5×–1.5× the tuned mean, capped below 1 so no chance is a certainty.
            double quality = _settings.MeanChanceQuality * (0.5 + rng.NextDouble());
            return Math.Min(quality, MaxChanceQuality);
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
