using System.Collections.Generic;
using Gaffer.Common;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The match simulation use case: command in → immutable outcome out (ARCHITECTURE §8). A thin
    /// orchestrator over injected steps — generate chances, resolve each to a goal or a miss — so
    /// tuning swaps a step without touching the pipeline (ARCHITECTURE §9). Deterministic: the same
    /// command and seed reproduce the same match (NON-NEGOTIABLE #2).
    /// </summary>
    public sealed class MatchSimulator
    {
        private readonly IChanceGenerator _chanceGenerator;
        private readonly IChanceResolver _chanceResolver;

        public MatchSimulator(IChanceGenerator chanceGenerator, IChanceResolver chanceResolver)
        {
            _chanceGenerator = chanceGenerator;
            _chanceResolver = chanceResolver;
        }

        public MatchOutcome Simulate(MatchCommand command, IRandom rng)
        {
            IReadOnlyList<Chance> chances = _chanceGenerator.GenerateChances(command, rng);

            var goals = new List<MatchEvent>();
            int homeGoals = 0;
            int awayGoals = 0;

            foreach (Chance chance in chances)
            {
                if (!_chanceResolver.ResolvesToGoal(chance, rng))
                {
                    continue;
                }

                goals.Add(new MatchEvent(chance.Minute, chance.Side, MatchEventKind.Goal));
                if (chance.Side == TeamSide.Home)
                {
                    homeGoals++;
                }
                else
                {
                    awayGoals++;
                }
            }

            goals.Sort((left, right) => left.Minute.CompareTo(right.Minute));
            return new MatchOutcome(homeGoals, awayGoals, goals);
        }
    }
}
