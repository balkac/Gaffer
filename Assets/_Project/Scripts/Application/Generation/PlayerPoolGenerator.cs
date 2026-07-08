using System;
using System.Collections.Generic;
using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Generates a run's pool of players and guarantees a few discoverable gems: the first players are
    /// drawn from a gem context (low visible ability, high hidden potential, young), then the rest from
    /// the ordinary context, and the pool is shuffled so gems are not at fixed slots. This is the
    /// [NON-NEGOTIABLE] discoverable-wonderkid guarantee (TDD §5) — the discovery fantasy is never left
    /// to chance, but stays rare enough to feel special. Deterministic through the injected rng.
    /// </summary>
    public sealed class PlayerPoolGenerator
    {
        private readonly IPlayerGenerator _playerGenerator;

        public PlayerPoolGenerator(IPlayerGenerator playerGenerator)
        {
            _playerGenerator = playerGenerator;
        }

        public IReadOnlyList<Player> GeneratePool(int poolSize, int guaranteedGems, GenerationContext ordinary, GenerationContext gem, IRandom rng)
        {
            int gems = Math.Min(Math.Max(guaranteedGems, 0), poolSize);
            var pool = new List<Player>(poolSize);
            for (int i = 0; i < poolSize; i++)
            {
                GenerationContext context = i < gems ? gem : ordinary;
                pool.Add(_playerGenerator.Generate(new PlayerId(i), context, rng));
            }

            Shuffle(pool, rng);
            return pool;
        }

        private static void Shuffle(List<Player> pool, IRandom rng)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                Player swap = pool[i];
                pool[i] = pool[j];
                pool[j] = swap;
            }
        }
    }
}
