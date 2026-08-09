using Gaffer.Domain.Players;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// One stacked morale entry as it stands right now: signed points on a player, and the weeks it still
    /// has to run. The ledger's own <c>Entry</c> stays private — this is the read model a save (or a debug
    /// view) sees, so exposing it cannot let anyone write to the ledger except through
    /// <see cref="MoraleLedger.Apply"/>.
    /// </summary>
    public readonly struct MoraleEntry
    {
        public MoraleEntry(PlayerId player, double points, int weeksLeft)
        {
            Player = player;
            Points = points;
            WeeksLeft = weeksLeft;
        }

        public PlayerId Player { get; }

        /// <summary>Signed: a wound is negative, a lift positive.</summary>
        public double Points { get; }

        /// <summary>Weeks REMAINING, not the original duration — the only figure a resume can put back.</summary>
        public int WeeksLeft { get; }
    }
}
