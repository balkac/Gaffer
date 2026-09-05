using Gaffer.Application.Transfers;

namespace Gaffer.Presentation.Market
{
    /// <summary>
    /// Whether a signing goes through, and by how much it misses, BEFORE the tap.
    ///
    /// <para>The playtest lesson this exists for: "I have cash but I can't sign him" — the rule was right
    /// and invisible until the button was pressed. So the card names the blocker up front, in the
    /// manager's own units. The two shortfalls are exactly the two refusals in
    /// <see cref="TransferService.Sign(Finances, Gaffer.Domain.Clubs.Squad, Gaffer.Domain.Players.Player, EconomySettings)"/>
    /// (<c>fee &gt; cash</c>, <c>wage &gt; headroom</c>), and a test holds them in step: the card must
    /// never promise what the core refuses, or refuse what it would take.</para>
    ///
    /// <para>Framework-free, so the bridge compiles and tests it (CLAUDE.md test bridge).</para>
    /// </summary>
    public readonly struct Affordability
    {
        private Affordability(long fee, long weeklyWage, long cashLeft, long wageRoomLeft)
        {
            Fee = fee;
            WeeklyWage = weeklyWage;
            CashLeft = cashLeft;
            WageRoomLeft = wageRoomLeft;
        }

        public static Affordability For(long fee, long weeklyWage, Finances finances)
        {
            return new Affordability(fee, weeklyWage, finances.Cash - fee, finances.WageHeadroom - weeklyWage);
        }

        public long Fee { get; }

        public long WeeklyWage { get; }

        /// <summary>Cash after the fee; negative by the amount missing.</summary>
        public long CashLeft { get; }

        /// <summary>Weekly wage room after his wage; negative by the amount missing.</summary>
        public long WageRoomLeft { get; }

        public bool CashShort => CashLeft < 0;

        public bool WageShort => WageRoomLeft < 0;

        public bool Affordable => !CashShort && !WageShort;
    }
}
