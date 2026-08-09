using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// <b>No free lunch.</b> A tactical setting must cost something. This is the guard for every axis on
    /// <see cref="Tactics"/> — not a patch for the one that was broken — and it fails if any option is
    /// <em>dominant</em>: at least as good as every alternative on that axis on every outcome the sim
    /// actually produces. A dominant option is not a decision; it is the answer, and the axis it sits on
    /// is decoration.
    ///
    /// <para><b>Why this file exists.</b> Tempo shipped with a volume multiplier and nothing else, so
    /// Intense was 15% more chances for free and Patient a pure loss — worse than not choosing tactics at
    /// all, since Balanced is the identity. Nothing failed, because nothing asserted it: every existing
    /// tactics test checked that an option <em>does something</em>, never that it <em>costs</em> something.
    /// Running the same question over a second axis caught a second one: a high press at
    /// <c>pressingDefenceStep</c> 0.04 measured +4.6% chances created against −0.9% conceded, because the
    /// possession the press wins takes the ball off the opponent and cancels the exposure it is supposed
    /// to pay for. Both are calibration bugs that only a measured, cross-option comparison can see.</para>
    ///
    /// <para><b>How it stays general.</b> The axes are discovered by reflection over the widest
    /// <see cref="Tactics"/> constructor — every enum parameter is an axis, every value of that enum an
    /// option — so an axis added tomorrow is held to the same rule the day it is written, with no edit
    /// here. Each option is played through the real <see cref="MatchSimulator"/> against an identical
    /// Balanced opponent, and the "outcomes" are what a manager would read off a season: chances created
    /// and conceded, goals scored and conceded, and how much a chance is worth (conversion). Conversion
    /// is a first-class metric on purpose — it is the whole cost side of a volume-for-quality trade, and
    /// without it every volume-raising option looks free.</para>
    /// </summary>
    public sealed class NoFreeLunchTests
    {
        // Enough matches that noise cannot invent a cost for an option that pays none. The tightest
        // metric is conversion (~27,000 goals per option here, so a standard error near 0.6%); the
        // smallest real trade on any axis is the ~4.7% chance-volume step between Standard and Press.
        private const int MatchesPerOption = 20000;
        private const ulong Seed = 20260808UL;

        // How much better one option must measure than another before the difference counts as a real
        // edge. Set well above the sampling noise at the match count above and well below every genuine
        // trade in the calibrated game — otherwise a dominant option could be let off by a metric that
        // merely wobbled in its favour. Both bounds are load-bearing: lower it and noise exonerates, raise
        // it past ~4% and a real trade stops counting.
        private const double MeaningfulEdge = 0.02;

        private static readonly Metric[] Metrics =
        {
            new Metric("chances", higherIsBetter: true, of: m => m.ChancesFor),
            new Metric("conversion", higherIsBetter: true, of: m => m.Conversion),
            new Metric("goals", higherIsBetter: true, of: m => m.GoalsFor),
            new Metric("conceded chances", higherIsBetter: false, of: m => m.ChancesAgainst),
            new Metric("conceded goals", higherIsBetter: false, of: m => m.GoalsAgainst),
        };

        [Test]
        public void TacticalAxes_AreDiscovered()
        {
            // A guard on the guard: if the reflection scan ever matched nothing, the dominance test below
            // would pass vacuously. Named rather than counted, so a fifth axis does not fail this — it
            // just gets measured.
            List<Axis> axes = DiscoverAxes();
            var names = new List<string>(axes.Count);
            for (int i = 0; i < axes.Count; i++)
            {
                names.Add(axes[i].Name);
                Assert.That(axes[i].Options.Count, Is.GreaterThan(1), axes[i].Name + " has nothing to choose between.");
            }

            Assert.That(names, Contains.Item("Mentality"), string.Join(", ", names));
            Assert.That(names, Contains.Item("Tempo"), string.Join(", ", names));
            Assert.That(names, Contains.Item("Pressing"), string.Join(", ", names));
            Assert.That(names, Contains.Item("Approach"), string.Join(", ", names));
        }

        [Test]
        public void EveryTacticalOption_IsBeatenOnSomeOutcome_SoNoneIsDominant()
        {
            // Every axis is measured before anything is asserted, so one broken axis does not hide the
            // next: a calibration pass gets the whole picture from one run.
            var dominant = new List<string>();
            foreach (Axis axis in DiscoverAxes())
            {
                var measured = new List<Measurement>(axis.Options.Count);
                for (int i = 0; i < axis.Options.Count; i++)
                {
                    measured.Add(Measure(axis.Options[i].Tactics));
                }

                TestContext.WriteLine(Table(axis, measured));

                for (int i = 0; i < measured.Count; i++)
                {
                    string beatenBy = LargestMeaningfulLoss(measured, i);
                    if (beatenBy == null)
                    {
                        dominant.Add($"{axis.Name}.{axis.Options[i].Name}\n" + Table(axis, measured));
                        continue;
                    }

                    TestContext.WriteLine($"    {axis.Name}.{axis.Options[i].Name} pays: {beatenBy}");
                }
            }

            Assert.That(dominant, Is.Empty,
                "A dominant tactical option is a free lunch: nothing else on its axis is meaningfully better " +
                "on any measured outcome, so it is not a choice, it is the answer, and the axis is decoration. " +
                "Give it a cost (the trade tempo and approach make: chance volume for chance quality) or drop " +
                "the option.\n" + string.Join("\n", dominant));
        }

        [Test]
        public void Tempo_TradesChanceVolumeForChanceQuality()
        {
            // The shape of the trade, named: the general test above proves *a* cost exists, this one
            // proves it is the intended one — an intense side hurries the extra chances it manufactures.
            Measurement intense = Measure(new Tactics(Mentality.Balanced, Tempo.Intense, Pressing.Standard));
            Measurement patient = Measure(new Tactics(Mentality.Balanced, Tempo.Patient, Pressing.Standard));

            Assert.That(intense.ChancesFor, Is.GreaterThan(patient.ChancesFor * 1.05),
                $"an intense tempo should make measurably more chances ({intense.ChancesFor:F3} vs {patient.ChancesFor:F3}).");
            Assert.That(intense.Conversion, Is.LessThan(patient.Conversion * 0.95),
                $"...and take them measurably worse ({intense.Conversion:F4} vs {patient.Conversion:F4}).");
        }

        [Test]
        public void ChanceProfileAxes_ChangeTheShapeOfChances_NotTheExpectedGoals()
        {
            // The calibration discipline behind the trade, asserted directly on the multipliers rather
            // than through 6,000 matches: volume x quality is what an option does to expected goals, and
            // every option holds it within 2% of neutral. This is what stops a "cost" from being either
            // a nerf or a buff wearing a trade's clothes.
            foreach (Tempo tempo in Enum.GetValues(typeof(Tempo)))
            {
                AssertProductIsNeutral("Tempo." + tempo, new Tactics(Mentality.Balanced, tempo, Pressing.Standard));
            }

            foreach (Approach approach in Enum.GetValues(typeof(Approach)))
            {
                AssertProductIsNeutral("Approach." + approach, new Tactics(Mentality.Balanced, Tempo.Standard, Pressing.Standard, approach));
            }
        }

        private static void AssertProductIsNeutral(string label, Tactics tactics)
        {
            var profile = ChanceProfile.FromTactics(tactics);
            double product = profile.Volume * profile.Quality;

            Assert.That(product, Is.EqualTo(1.0).Within(0.02),
                $"{label} multiplies expected goals by {product:F4} ({profile.Volume:F3} volume x " +
                $"{profile.Quality:F3} quality). A chance-profile option must change the shape of a side's " +
                "chances, not how many goals it expects — pair every volume change with its quality opposite.");
        }

        // --- Measuring one option -------------------------------------------------------------------

        /// <summary>
        /// Plays one option through the real simulator: the same eleven on both sides, the tactic on the
        /// home side only, a Balanced opponent, and the shipped calibration. Tactics reach the sim by both
        /// routes the game uses — the strength axes through <see cref="EffectiveStrengthBuilder"/>
        /// (mentality, pressing) and the chance profile through <see cref="ChanceProfile.FromTactics(Tactics)"/>
        /// (tempo, approach) — so an axis is measured however it happens to work.
        /// </summary>
        private static Measurement Measure(Tactics tactics)
        {
            var builder = new EffectiveStrengthBuilder();
            IReadOnlyList<Player> eleven = Eleven();
            var command = new MatchCommand(
                builder.Build(eleven, tactics),
                builder.Build(eleven, Tactics.Balanced),
                null,
                null,
                ChanceProfile.FromTactics(tactics),
                ChanceProfile.Neutral,
                default);

            var simulator = new MatchSimulator(new PoissonChanceGenerator(MatchSimulationSettings.Default), new QualityChanceResolver());
            var rng = new SplitMix64RandomNumberGenerator(Seed);
            long chancesFor = 0;
            long chancesAgainst = 0;
            long goalsFor = 0;
            long goalsAgainst = 0;
            for (int i = 0; i < MatchesPerOption; i++)
            {
                MatchOutcome outcome = simulator.Simulate(command, rng);
                chancesFor += outcome.HomeShots;
                chancesAgainst += outcome.AwayShots;
                goalsFor += outcome.HomeGoals;
                goalsAgainst += outcome.AwayGoals;
            }

            return new Measurement(
                (double)chancesFor / MatchesPerOption,
                (double)goalsFor / MatchesPerOption,
                (double)chancesAgainst / MatchesPerOption,
                (double)goalsAgainst / MatchesPerOption,
                chancesFor > 0 ? (double)goalsFor / chancesFor : 0.0);
        }

        /// <summary>
        /// The outcome on which this option loses by the most to some alternative on the same axis — the
        /// cost it pays, quoted at its clearest. Null means nothing beats it anywhere by more than
        /// <see cref="MeaningfulEdge"/>: a dominant option.
        /// </summary>
        private static string LargestMeaningfulLoss(IReadOnlyList<Measurement> measured, int index)
        {
            string worst = null;
            double worstMargin = MeaningfulEdge;
            for (int m = 0; m < Metrics.Length; m++)
            {
                Metric metric = Metrics[m];
                double mine = metric.Of(measured[index]);
                if (mine <= 0.0)
                {
                    continue;
                }

                for (int other = 0; other < measured.Count; other++)
                {
                    if (other == index)
                    {
                        continue;
                    }

                    double theirs = metric.Of(measured[other]);
                    double margin = metric.HigherIsBetter ? (theirs / mine) - 1.0 : 1.0 - (theirs / mine);
                    if (margin > worstMargin)
                    {
                        worstMargin = margin;
                        worst = string.Format(
                            CultureInfo.InvariantCulture,
                            "{0} {1:F4} against {2:F4} ({3:P1} worse)",
                            metric.Name,
                            mine,
                            theirs,
                            margin);
                    }
                }
            }

            return worst;
        }

        private static string Table(Axis axis, IReadOnlyList<Measurement> measured)
        {
            var text = new StringBuilder();
            text.AppendLine(axis.Name + " — " + MatchesPerOption + " matches per option, identical squads, Balanced opponent");
            for (int i = 0; i < measured.Count; i++)
            {
                Measurement m = measured[i];
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0,-16} chances {1,6:F3}  conversion {2,6:F4}  goals {3,6:F3}  |  conceded {4,6:F3} chances, {5,6:F3} goals",
                    axis.Options[i].Name,
                    m.ChancesFor,
                    m.Conversion,
                    m.GoalsFor,
                    m.ChancesAgainst,
                    m.GoalsAgainst));
            }

            return text.ToString();
        }

        // --- Discovering the axes -------------------------------------------------------------------

        /// <summary>
        /// Every enum parameter of the widest <see cref="Tactics"/> constructor is an axis, and every
        /// value of that enum an option; the other parameters stay at <see cref="Tactics.Balanced"/>, so
        /// each option is measured against the neutral setup exactly as a manager would change one dial.
        /// Reflection rather than a hand-kept list, so this test covers a fifth axis without being edited.
        /// </summary>
        private static List<Axis> DiscoverAxes()
        {
            ConstructorInfo widest = null;
            foreach (ConstructorInfo candidate in typeof(Tactics).GetConstructors())
            {
                if (widest == null || candidate.GetParameters().Length > widest.GetParameters().Length)
                {
                    widest = candidate;
                }
            }

            ParameterInfo[] parameters = widest.GetParameters();
            object[] balanced = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                PropertyInfo property = typeof(Tactics).GetProperty(
                    char.ToUpperInvariant(parameters[i].Name[0]) + parameters[i].Name.Substring(1));
                Assert.That(property, Is.Not.Null,
                    "Tactics constructor parameter '" + parameters[i].Name + "' has no matching property, so " +
                    "the axis scan cannot hold the other axes neutral while it varies this one.");
                balanced[i] = property.GetValue(Tactics.Balanced);
            }

            var axes = new List<Axis>(parameters.Length);
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!parameters[i].ParameterType.IsEnum)
                {
                    continue;
                }

                var options = new List<Option>();
                foreach (object value in Enum.GetValues(parameters[i].ParameterType))
                {
                    object[] args = (object[])balanced.Clone();
                    args[i] = value;
                    options.Add(new Option(value.ToString(), (Tactics)widest.Invoke(args)));
                }

                axes.Add(new Axis(parameters[i].ParameterType.Name, options));
            }

            return axes;
        }

        // --- Fixtures -------------------------------------------------------------------------------

        private static IReadOnlyList<Player> Eleven()
        {
            PlayerRole[] roles =
            {
                PlayerRole.Goalkeeper,
                PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
                PlayerRole.RightMidfield, PlayerRole.CentralMidfield, PlayerRole.CentralMidfield, PlayerRole.LeftMidfield,
                PlayerRole.Striker, PlayerRole.Striker,
            };

            var players = new List<Player>(roles.Length);
            for (int i = 0; i < roles.Length; i++)
            {
                players.Add(new Player(new PlayerId(i + 1), "P" + i, "England", roles[i], 26, Uniform(60), 75, null));
            }

            return players;
        }

        private static Attributes Uniform(byte stat)
        {
            return new Attributes
            {
                Finishing = stat,
                Technique = stat,
                FirstTouch = stat,
                Dribbling = stat,
                Passing = stat,
                Crossing = stat,
                Heading = stat,
                LongShots = stat,
                Marking = stat,
                Tackling = stat,
                Penalties = stat,
                FreeKicks = stat,
                Corners = stat,
                LongThrows = stat,
                Pace = stat,
                Acceleration = stat,
                Stamina = stat,
                Strength = stat,
                Agility = stat,
                Jumping = stat,
                Balance = stat,
                Positioning = stat,
                Reflexes = stat,
                Handling = stat,
                AerialReach = stat,
                CommandOfArea = stat,
                OneOnOnes = stat,
                Kicking = stat,
                GkPositioning = stat,
            };
        }

        private readonly struct Measurement
        {
            public Measurement(double chancesFor, double goalsFor, double chancesAgainst, double goalsAgainst, double conversion)
            {
                ChancesFor = chancesFor;
                GoalsFor = goalsFor;
                ChancesAgainst = chancesAgainst;
                GoalsAgainst = goalsAgainst;
                Conversion = conversion;
            }

            public double ChancesFor { get; }

            public double GoalsFor { get; }

            public double ChancesAgainst { get; }

            public double GoalsAgainst { get; }

            /// <summary>Goals per chance created — what one chance is worth to this side.</summary>
            public double Conversion { get; }
        }

        private sealed class Metric
        {
            public Metric(string name, bool higherIsBetter, Func<Measurement, double> of)
            {
                Name = name;
                HigherIsBetter = higherIsBetter;
                Of = of;
            }

            public string Name { get; }

            public bool HigherIsBetter { get; }

            public Func<Measurement, double> Of { get; }
        }

        private sealed class Option
        {
            public Option(string name, Tactics tactics)
            {
                Name = name;
                Tactics = tactics;
            }

            public string Name { get; }

            public Tactics Tactics { get; }
        }

        private sealed class Axis
        {
            public Axis(string name, IReadOnlyList<Option> options)
            {
                Name = name;
                Options = options;
            }

            public string Name { get; }

            public IReadOnlyList<Option> Options { get; }
        }
    }
}
