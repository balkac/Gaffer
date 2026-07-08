using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Builds a club's <see cref="Squad"/> with a believable line-up — a set number of keepers,
    /// defenders, midfielders, and forwards, not four uniformly-random positions — so a roster reads
    /// like a real team and the strength builder finds every line manned. Player ids are handed out
    /// from <c>firstPlayerId</c> so each club's players stay unique across the league. Deterministic
    /// through the injected rng. Formations and squad depth tune later; this is a standard senior squad.
    /// </summary>
    public sealed class SquadGenerator
    {
        public const int Goalkeepers = 2;
        public const int Defenders = 6;
        public const int Midfielders = 7;
        public const int Forwards = 5;
        public const int SquadSize = Goalkeepers + Defenders + Midfielders + Forwards;

        private readonly IPlayerGenerator _playerGenerator;

        public SquadGenerator(IPlayerGenerator playerGenerator)
        {
            _playerGenerator = playerGenerator;
        }

        public Squad Generate(int firstPlayerId, GenerationContext context, IRandom rng)
        {
            var players = new Player[SquadSize];
            int slot = 0;

            slot = AppendLine(players, slot, firstPlayerId, Position.Goalkeeper, Goalkeepers, context, rng);
            slot = AppendLine(players, slot, firstPlayerId, Position.Defender, Defenders, context, rng);
            slot = AppendLine(players, slot, firstPlayerId, Position.Midfielder, Midfielders, context, rng);
            AppendLine(players, slot, firstPlayerId, Position.Forward, Forwards, context, rng);

            return new Squad(players);
        }

        private int AppendLine(Player[] players, int slot, int firstPlayerId, Position position, int count, GenerationContext context, IRandom rng)
        {
            for (int i = 0; i < count; i++)
            {
                var id = new PlayerId(firstPlayerId + slot);
                players[slot] = _playerGenerator.Generate(id, context, position, rng);
                slot++;
            }

            return slot;
        }
    }
}
