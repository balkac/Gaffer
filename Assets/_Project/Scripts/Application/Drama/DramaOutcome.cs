using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// What resolving a drama choice did — morale is already applied to the ledger the resolver was
    /// handed; the parts owned elsewhere come back as data for their owners to execute (cash into
    /// the finances, a forced sale through the transfer service). Command in, outcome out.
    /// </summary>
    public sealed class DramaOutcome
    {
        public DramaOutcome(DramaEventId eventId, int choiceIndex, long cashDelta, Player playerToSell)
        {
            EventId = eventId;
            ChoiceIndex = choiceIndex;
            CashDelta = cashDelta;
            PlayerToSell = playerToSell;
        }

        public DramaEventId EventId { get; }

        public int ChoiceIndex { get; }

        /// <summary>Signed cash consequence for the club's finances (fines in, sweeteners and cuts out).</summary>
        public long CashDelta { get; }

        /// <summary>Set when the choice sells the subject; the caller executes the transfer it owns.</summary>
        public Player PlayerToSell { get; }
    }
}
