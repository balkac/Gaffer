using System;
using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Pins the scorer draw against the straight-line implementation it was optimised out of.
    /// <see cref="WeightedScorerSelector"/> now memoises a cumulative weight vector per squad and binary-
    /// searches it instead of walking the roster twice per goal; determinism is a project
    /// NON-NEGOTIABLE, so "faster" is only acceptable if it names the *same player for the same roll*.
    /// The oracle below is the pre-optimisation algorithm, transcribed expression for expression so its
    /// floating-point accumulation order matches — a different summation order would be a different
    /// answer at the boundaries, which is exactly what these tests exist to catch.
    /// </summary>
    public sealed class WeightedScorerSelectorEquivalenceTests
    {
        // ----- The oracle: the linear two-pass scan, verbatim -------------------------------------------

        private static double ReferenceWeight(Player player, ScorerWeights w)
        {
            Attributes a = player.Attributes;
            double openPlay = ((w.OpenPlayFinishing * a.Finishing) + (w.OpenPlayPositioning * a.Positioning) + (w.OpenPlayPace * a.Pace)) * ReferenceOpenPlayRole(player.Position, w);
            double aerial = ((w.AerialHeading * a.Heading) + (w.AerialJumping * a.Jumping) + (w.AerialStrength * a.Strength)) * ReferenceAerialRole(player.Position, w);
            double weight = openPlay + aerial;
            return player.Position == Position.Goalkeeper ? weight : Math.Max(w.MinOutfielderWeight, weight);
        }

        private static double ReferenceOpenPlayRole(Position position, ScorerWeights w)
        {
            switch (position)
            {
                case Position.Forward:
                    return w.OpenPlayForward;
                case Position.Midfielder:
                    return w.OpenPlayMidfielder;
                case Position.Defender:
                    return w.OpenPlayDefender;
                case Position.Goalkeeper:
                    return w.OpenPlayGoalkeeper;
                default:
                    return 0.3;
            }
        }

        private static double ReferenceAerialRole(Position position, ScorerWeights w)
        {
            switch (position)
            {
                case Position.Forward:
                    return w.AerialForward;
                case Position.Midfielder:
                    return w.AerialMidfielder;
                case Position.Defender:
                    return w.AerialDefender;
                case Position.Goalkeeper:
                    return w.AerialGoalkeeper;
                default:
                    return 0.2;
            }
        }

        private static PlayerId? ReferenceSelect(Squad squad, IRandom rng, ScorerWeights w)
        {
            if (squad == null || squad.Count == 0)
            {
                return null;
            }

            IReadOnlyList<Player> players = squad.Players;

            double total = 0.0;
            for (int i = 0; i < players.Count; i++)
            {
                total += ReferenceWeight(players[i], w);
            }

            double roll = rng.NextDouble() * total;
            double cumulative = 0.0;
            for (int i = 0; i < players.Count; i++)
            {
                cumulative += ReferenceWeight(players[i], w);
                if (roll < cumulative)
                {
                    return players[i].Id;
                }
            }

            return players[players.Count - 1].Id;
        }

        // ----- Fixtures ---------------------------------------------------------------------------------

        private static Squad GeneratedSquad(int idBase, ulong seed)
        {
            return new SquadGenerator(new PlayerGenerator())
                .Generate(idBase, new GenerationContext(), new SplitMix64RandomNumberGenerator(seed));
        }

        private static Player PlayerAt(int id, Position position, byte finishing, byte heading)
        {
            var attributes = new Attributes
            {
                Finishing = finishing,
                Positioning = 60,
                Pace = 60,
                Heading = heading,
                Jumping = 55,
                Strength = 58,
            };
            return new Player(new PlayerId(id), "Player " + id, "England", position, 24, attributes, 70);
        }

        private static void AssertMatchesOracle(IReadOnlyList<Squad> squads, int draws, ulong seed, ScorerWeights weights)
        {
            var selector = new WeightedScorerSelector(weights);
            var actualRng = new SplitMix64RandomNumberGenerator(seed);
            var oracleRng = new SplitMix64RandomNumberGenerator(seed);

            for (int i = 0; i < draws; i++)
            {
                // Cycling the squads is not incidental: a match alternates between two rosters, so the
                // memo has to survive being asked for the other side's squad in between, and a third
                // roster has to evict cleanly rather than answer with a stale vector.
                Squad squad = squads[i % squads.Count];
                PlayerId? actual = selector.SelectScorer(squad, actualRng);
                PlayerId? expected = ReferenceSelect(squad, oracleRng, weights);

                Assert.That(actual?.Value, Is.EqualTo(expected?.Value), $"Draw {i} over squad {i % squads.Count} picked a different scorer than the linear scan.");
            }
        }

        // ----- Tests ------------------------------------------------------------------------------------

        [Test]
        public void SelectScorer_AlternatingBetweenTwoSquads_PicksExactlyWhatTheLinearScanPicked()
        {
            var squads = new List<Squad>
            {
                GeneratedSquad(0, 7UL),
                GeneratedSquad(500, 31UL),
            };

            AssertMatchesOracle(squads, draws: 4000, seed: 2026UL, weights: ScorerWeights.Default);
        }

        [Test]
        public void SelectScorer_MoreSquadsThanCacheSlots_PicksExactlyWhatTheLinearScanPicked()
        {
            // Three rosters against a two-slot memo: every call is a miss, so this is the rebuild path.
            var squads = new List<Squad>
            {
                GeneratedSquad(0, 11UL),
                GeneratedSquad(500, 12UL),
                GeneratedSquad(1000, 13UL),
            };

            AssertMatchesOracle(squads, draws: 3000, seed: 99UL, weights: ScorerWeights.Default);
        }

        [Test]
        public void SelectScorer_SquadsOfDifferentSizes_PicksExactlyWhatTheLinearScanPicked()
        {
            // A shorter roster reusing a slot sized for a longer one — the case where a stale tail in the
            // reused array would be read if the valid length were not tracked.
            var squads = new List<Squad>
            {
                GeneratedSquad(0, 5UL),
                new Squad(new List<Player> { PlayerAt(9001, Position.Forward, 80, 50) }),
                new Squad(new List<Player>
                {
                    PlayerAt(9100, Position.Goalkeeper, 12, 40),
                    PlayerAt(9101, Position.Defender, 40, 86),
                    PlayerAt(9102, Position.Forward, 84, 62),
                }),
            };

            AssertMatchesOracle(squads, draws: 3000, seed: 4242UL, weights: ScorerWeights.Default);
        }

        [Test]
        public void SelectScorer_WithZeroedPathwayWeights_PicksExactlyWhatTheLinearScanPicked()
        {
            // Ties are the boundary case: with the pathways zeroed every outfielder sits exactly on the
            // floor, so the cumulative vector has runs of equal steps and the binary search must still
            // land on the FIRST index whose cumulative total exceeds the roll — what `roll < cumulative`
            // meant in the linear scan.
            var weights = new ScorerWeights
            {
                OpenPlayFinishing = 0.0,
                OpenPlayPositioning = 0.0,
                OpenPlayPace = 0.0,
                AerialHeading = 0.0,
                AerialJumping = 0.0,
                AerialStrength = 0.0,
                MinOutfielderWeight = 1.0,
            };
            var squads = new List<Squad> { GeneratedSquad(0, 3UL), GeneratedSquad(500, 4UL) };

            AssertMatchesOracle(squads, draws: 4000, seed: 555UL, weights: weights);
        }

        [Test]
        public void SelectScorer_AfterARosterChange_UsesTheNewSquadsWeights()
        {
            // Squad is immutable, so a changed roster is a new instance — which is what makes memoising by
            // instance sound. This pins the property the memo depends on.
            Squad before = GeneratedSquad(0, 21UL);
            Squad after = before.Remove(before.Players[0].Id).Add(PlayerAt(9500, Position.Forward, 99, 90));

            Assert.That(ReferenceEquals(before, after), Is.False);
            AssertMatchesOracle(new List<Squad> { before, after }, draws: 2000, seed: 606UL, weights: ScorerWeights.Default);
        }
    }
}
