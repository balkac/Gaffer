namespace Gaffer.Application.Rivals
{
    /// <summary>
    /// How the clubs you are not managing behave — the balance <see cref="RivalManager"/> reads
    /// (NON-NEGOTIABLE #3). Immutable, all-optional constructor, cached <see cref="Default"/>.
    /// </summary>
    public sealed class RivalSettings
    {
        public RivalSettings(
            long transferBudgetPerStrengthPoint = 900_000L,
            double budgetStrengthFloor = 45.0,
            int maxSigningsPerSeason = 2,
            int shortlistSize = 60,
            double minimumImprovement = 2.0)
        {
            TransferBudgetPerStrengthPoint = transferBudgetPerStrengthPoint;
            BudgetStrengthFloor = budgetStrengthFloor;
            MaxSigningsPerSeason = maxSigningsPerSeason < 0 ? 0 : maxSigningsPerSeason;
            ShortlistSize = shortlistSize < 1 ? 1 : shortlistSize;
            MinimumImprovement = minimumImprovement;
        }

        /// <summary>
        /// What one point of team strength above the floor is worth in transfer money.
        ///
        /// <para>A DERIVED budget, not a ledger. The owner deferred the economy deliberately — wages,
        /// contracts and income are to be designed together, FM-style — so giving rival clubs a real
        /// balance sheet now would force that design early and half-finished. One number off strength
        /// gets the thing that actually matters into the game: a big club outbids you for the boy you
        /// were watching. When the real economy lands, this is the seam it replaces.</para>
        /// </summary>
        public long TransferBudgetPerStrengthPoint { get; }

        /// <summary>The strength a club spends nothing at. Below it there is no budget at all.</summary>
        public double BudgetStrengthFloor { get; }

        /// <summary>
        /// How many players a club will sign in one summer. Small on purpose: rivals are meant to take
        /// players off the board, not clear it. A market the manager finds already emptied is not
        /// competition, it is a locked door.
        /// </summary>
        public int MaxSigningsPerSeason { get; }

        /// <summary>
        /// How many of the market's best players — by VISIBLE ability — the rivals collectively shop from.
        ///
        /// <para><b>The top, not a random sample, and that is the design.</b> A random sample was tried
        /// and measured: at the shipped scale rivals took 30 players out of 50,000, never touched the best
        /// one on the board, and left 1,500 high-potential teenagers untouched every season. Competition
        /// nobody can feel is not competition. Shopping the top means they take players the manager
        /// actually wanted.</para>
        ///
        /// <para><b>And it puts the manager's edge exactly where the game wants it.</b> Rivals buy on
        /// current ability, which is the number everyone can see. They do not chase POTENTIAL — that is
        /// scout-masked (TDD §5), and seeing what they cannot is the whole discover-grow-sell fantasy
        /// (GDD §4.4). The cheap sixteen-year-old with a hidden ceiling is left for you, and the polished
        /// twenty-six-year-old is not.</para>
        ///
        /// <para>Cost is one partial pass over the market per window, shared by every club — not one per
        /// club, which at 50,000 would be a million evaluations.</para>
        /// </summary>
        public int ShortlistSize { get; }

        /// <summary>
        /// How much better a player must be than what the club already has in that role before it will
        /// pay for him. Without it a club churns its squad every summer for nothing.
        /// </summary>
        public double MinimumImprovement { get; }

        public static RivalSettings Default { get; } = new RivalSettings();
    }
}
