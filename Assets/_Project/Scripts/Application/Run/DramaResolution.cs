using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Transfers;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// What answering a drama did, once — every consequence, applied in one ordered step and reported
    /// together (ARCHITECTURE §8/§8a). The morale entries are here too: a UI cannot replay a change it
    /// was never told about, and while the resolver applied them to the ledger in place there was no
    /// honest way to report them at all.
    /// <para><b>All or nothing.</b> This record only ever describes a resolution that fully happened. If
    /// the choice forces a sale the squad cannot make, <see cref="RunSession.ResolveDrama"/> returns a
    /// failure with <em>nothing</em> applied and the event still pending, rather than a partial record —
    /// see that method for why.</para>
    /// </summary>
    public sealed class DramaResolution
    {
        public DramaEventId EventId { get; init; }

        public int ChoiceIndex { get; init; }

        /// <summary>The signed cash consequence of the choice itself (excluding any sale fee).</summary>
        public long CashDelta { get; init; }

        /// <summary>The club's money after the cash effect and any forced sale.</summary>
        public Finances Finances { get; init; }

        /// <summary>The morale entries that landed on the ledger, in the order they were applied.</summary>
        public IReadOnlyList<MoraleChange> MoraleChanges { get; init; }

        /// <summary>The player the choice forced out, or null. He is back on the market.</summary>
        public Player SoldPlayer { get; init; }

        /// <summary>What the forced sale brought in; 0 when there was none.</summary>
        public long SaleFee { get; init; }

        /// <summary>The teammate a trait passed to, as he was before, or null.</summary>
        public Player TraitGrantTarget { get; init; }

        /// <summary>The trait he received.</summary>
        public TraitId GrantedTrait { get; init; }

        /// <summary>The same player rebuilt with the trait and swapped into the live squad, or null.</summary>
        public Player RebuiltPlayer { get; init; }

        /// <summary>
        /// The team sheet after the resolution — re-picked when the squad changed, otherwise the sheet
        /// as it stood. Never null, so a view has one thing to draw either way.
        /// </summary>
        public LineupOutcome Lineup { get; init; }
    }
}
