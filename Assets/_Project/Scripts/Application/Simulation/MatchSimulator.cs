using System;
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
        // PERFORMANCE §8, verified against this Unity version's shipped IL: List<T>.Sort(Comparison<T>)
        // with a *cached* delegate is the one allocation-free overload — the comparison is passed raw
        // through the sort, no wrapper object. Sort() and Sort(IComparer<T>) are the trap: both allocate
        // a Comparison<T> per call from the comparer.Compare method-group conversion inside
        // ArraySortHelper. Cached in a static readonly field because C# 9 caches no method group
        // (C# 11 would cache the static one; Unity 6 is C# 9).
        private static readonly Comparison<MatchEvent> ByMinute = CompareByMinute;

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

                IReadOnlyList<Player> onThePitch = chance.Side == TeamSide.Home ? command.HomeEleven : command.AwayEleven;
                PlayerId? scorer = onThePitch != null ? _scorerSelector.SelectScorer(onThePitch, rng) : null;

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

        // Orders the goals as they happened. Deliberately keyed on the minute alone: two goals in the
        // same minute compare equal, and the sort (introsort, unstable on both overloads) settles them
        // by the same deterministic swap sequence for a given input — so the same seed still reproduces
        // the same event list. Adding a tie-break here would reorder same-minute goals, which is a
        // behaviour change, not an optimisation.
        private static int CompareByMinute(MatchEvent left, MatchEvent right)
        {
            return left.Minute.CompareTo(right.Minute);
        }
    }
}
