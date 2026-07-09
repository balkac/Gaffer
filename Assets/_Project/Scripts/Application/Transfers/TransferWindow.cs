namespace Gaffer.Application.Transfers
{
    /// <summary>Which transfer window (if any) is open at a point in the season.</summary>
    public enum TransferWindowPhase
    {
        /// <summary>No window open — the squad is locked until the next window.</summary>
        Closed = 0,

        /// <summary>Pre-season: before the first round is played.</summary>
        Summer = 1,

        /// <summary>Mid-season break: at the halfway point of the fixture list.</summary>
        Winter = 2,
    }

    /// <summary>
    /// Decides when transfers may happen (decision: sezon başı / devre arası). A signing or sale is only
    /// allowed in an open window, so the manager builds the squad before kickoff and can only reshape it again
    /// at the winter break — the rest of the season is played with what he has. Pure and deterministic: the
    /// window is a function of how many rounds have been played, nothing else.
    /// </summary>
    public static class TransferWindow
    {
        /// <summary>
        /// The window open when <paramref name="playedRounds"/> rounds are done and <paramref name="roundCount"/>
        /// is the season's total. Summer is open before kickoff (nothing played); Winter opens for the single
        /// break at the halfway point; otherwise the market is closed.
        /// </summary>
        public static TransferWindowPhase At(int playedRounds, int roundCount)
        {
            if (playedRounds <= 0)
            {
                return TransferWindowPhase.Summer;
            }

            if (roundCount > 0 && playedRounds == roundCount / 2)
            {
                return TransferWindowPhase.Winter;
            }

            return TransferWindowPhase.Closed;
        }

        /// <summary>True when a transfer may be made at this point in the season.</summary>
        public static bool IsOpen(int playedRounds, int roundCount)
        {
            return At(playedRounds, roundCount) != TransferWindowPhase.Closed;
        }
    }
}
