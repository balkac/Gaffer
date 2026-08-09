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

        // A cap below 1 is incoherent — the cap is also reciprocated into the ratio floor, so the floor
        // would sit above the ceiling — and a cap of 0 makes that floor infinite, which drives the
        // expected count to infinity without throwing anything (CONVENTIONS §6). 1 is the true lower
        // bound; the calibrated 2.0 is untouched.
        private const double MinStrengthRatioCap = 1.0;

        // The Knuth loop below runs on the order of lambda iterations, so a runaway expected count is
        // not an exception, it is ~750 bogus chances in one match. At the calibrated settings a side
        // expects at most ~21, so this cap is a backstop that can never act as a balance dial.
        private const double MaxExpectedChances = 100.0;

        private readonly MatchSimulationSettings _settings;

        // Scratch buffer reused across matches (cleared per call) so the per-match path allocates
        // nothing (PERFORMANCE §8). Returning it is allowed by the buffer-lifetime rule stated on
        // IChanceGenerator.GenerateChances, which owns that constraint (ARCHITECTURE §8a).
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
            double strengthRatio = opponentDefence <= 0.0 ? StrengthRatioCap : ClampRatio(attack / opponentDefence);
            double expectedChances = _settings.BaseChancesPerTeam * (2.0 * possession) * strengthRatio * advantage * profile.Volume;

            int count = SamplePoisson(ClampExpectedChances(expectedChances), rng);
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

        /// <summary>
        /// The configured mismatch cap, held at or above 1 — the divisor guard for the ratio floor
        /// below (CONVENTIONS §6). A NaN fails the comparison and falls to the bound, by design.
        /// </summary>
        private double StrengthRatioCap
        {
            get
            {
                double cap = _settings.MaxStrengthRatio;
                return cap >= MinStrengthRatioCap ? cap : MinStrengthRatioCap;
            }
        }

        private double ClampRatio(double ratio)
        {
            double cap = StrengthRatioCap;
            double floor = 1.0 / cap;

            // Written as a failed lower-bound test rather than `ratio < floor` so a NaN ratio (from a
            // poisoned strength axis) lands on the floor instead of passing straight through.
            if (!(ratio > floor))
            {
                return floor;
            }

            return ratio > cap ? cap : ratio;
        }

        // The last gate before the sampling loop: whatever the settings and the strength axes produced,
        // the expected count that reaches Knuth's algorithm is a finite, bounded number.
        private static double ClampExpectedChances(double expected)
        {
            if (!(expected > 0.0))
            {
                return 0.0;
            }

            return expected > MaxExpectedChances ? MaxExpectedChances : expected;
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
