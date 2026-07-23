using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The match simulation use case: command in → immutable outcome out (ARCHITECTURE §8). A thin
    /// orchestrator over injected steps — generate chances, resolve each to a goal or a miss, credit a
    /// scorer — so tuning swaps a step without touching the pipeline (ARCHITECTURE §9). Deterministic:
    /// the same command and seed reproduce the same match (NON-NEGOTIABLE #2). Scorer attribution only
    /// draws from the rng when the command carries a squad, so strength-only matches are unaffected.
    /// </summary>
    public sealed class MatchSimulator
    {
        // Sort(Comparison<T>) wraps the delegate in a fresh comparer per call on Mono; a cached
        // IComparer<T> singleton keeps the per-match path allocation-free (PERFORMANCE §8).
        private static readonly IComparer<MatchEvent> ByMinute = new MinuteComparer();

        private readonly IChanceGenerator _chanceGenerator;
        private readonly IChanceResolver _chanceResolver;
        private readonly IScorerSelector _scorerSelector;

        public MatchSimulator(IChanceGenerator chanceGenerator, IChanceResolver chanceResolver)
            : this(chanceGenerator, chanceResolver, new WeightedScorerSelector())
        {
        }

        public MatchSimulator(IChanceGenerator chanceGenerator, IChanceResolver chanceResolver, IScorerSelector scorerSelector)
        {
            _chanceGenerator = chanceGenerator;
            _chanceResolver = chanceResolver;
            _scorerSelector = scorerSelector;
        }

        public MatchOutcome Simulate(MatchCommand command, IRandom rng)
        {
            IReadOnlyList<Chance> chances = _chanceGenerator.GenerateChances(command, rng);

            var goals = new List<MatchEvent>(8);
            int homeGoals = 0;
            int awayGoals = 0;
            int homeShots = 0;
            int awayShots = 0;

            for (int i = 0; i < chances.Count; i++)
            {
                Chance chance = chances[i];
                if (chance.Side == TeamSide.Home)
                {
                    homeShots++;
                }
                else
                {
                    awayShots++;
                }

                if (!_chanceResolver.ResolvesToGoal(chance, rng))
                {
                    continue;
                }

                Squad scoringSquad = chance.Side == TeamSide.Home ? command.HomeSquad : command.AwaySquad;
                PlayerId? scorer = scoringSquad != null ? _scorerSelector.SelectScorer(scoringSquad, rng) : null;

                goals.Add(new MatchEvent(chance.Minute, chance.Side, MatchEventKind.Goal, scorer));
                if (chance.Side == TeamSide.Home)
                {
                    homeGoals++;
                }
                else
                {
                    awayGoals++;
                }
            }

            goals.Sort(ByMinute);
            return new MatchOutcome(homeGoals, awayGoals, homeShots, awayShots, goals);
        }

        private sealed class MinuteComparer : IComparer<MatchEvent>
        {
            public int Compare(MatchEvent left, MatchEvent right)
            {
                return left.Minute.CompareTo(right.Minute);
            }
        }
    }
}
