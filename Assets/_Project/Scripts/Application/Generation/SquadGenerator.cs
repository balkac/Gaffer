using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// Builds a club's <see cref="Squad"/> with a believable roster of specific roles — two keepers, a
    /// right-back, four centre-backs, and so on — so a formation finds a real player for every slot and the
    /// strength builder finds every line manned. The role spread still sums to the standard line totals
    /// (2 GK / 6 DEF / 7 MID / 5 FWD). Player ids are handed out from <c>firstPlayerId</c> so each club's
    /// players stay unique across the league. Deterministic through the injected rng.
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

            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.Goalkeeper, 2, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.RightBack, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.CentreBack, 4, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.LeftBack, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.DefensiveMidfield, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.CentralMidfield, 3, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.AttackingMidfield, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.RightMidfield, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.LeftMidfield, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.RightWing, 1, context, rng);
            slot = AppendRole(players, slot, firstPlayerId, PlayerRole.LeftWing, 1, context, rng);
            AppendRole(players, slot, firstPlayerId, PlayerRole.Striker, 3, context, rng);

            return new Squad(players);
        }

        private int AppendRole(Player[] players, int slot, int firstPlayerId, PlayerRole role, int count, GenerationContext context, IRandom rng)
        {
            for (int i = 0; i < count; i++)
            {
                var id = new PlayerId(firstPlayerId + slot);
                players[slot] = _playerGenerator.Generate(id, context, role, rng);
                slot++;
            }

            return slot;
        }
    }
}
