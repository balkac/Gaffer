using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// What resolving a drama choice did — morale is already applied to the ledger the resolver was
    /// handed; the parts owned elsewhere come back as data for their owners to execute (cash into
    /// the finances, a forced sale through the transfer service, a granted trait onto the rebuilt
    /// player). Command in, outcome out.
    /// </summary>
    public sealed class DramaOutcome
    {
        public DramaOutcome(DramaEventId eventId, int choiceIndex, long cashDelta, Player playerToSell, Player traitGrantTarget = null, TraitId grantedTrait = default)
        {
            EventId = eventId;
            ChoiceIndex = choiceIndex;
            CashDelta = cashDelta;
            PlayerToSell = playerToSell;
            TraitGrantTarget = traitGrantTarget;
            GrantedTrait = grantedTrait;
        }

        public DramaEventId EventId { get; }

        public int ChoiceIndex { get; }

        /// <summary>Signed cash consequence for the club's finances (fines in, sweeteners and cuts out).</summary>
        public long CashDelta { get; }

        /// <summary>Set when the choice sells the subject; the caller executes the transfer it owns.</summary>
        public Player PlayerToSell { get; }

        /// <summary>Set when a trait passes to a teammate (the anointed successor); the caller rebuilds him.</summary>
        public Player TraitGrantTarget { get; }

        /// <summary>The trait <see cref="TraitGrantTarget"/> receives.</summary>
        public TraitId GrantedTrait { get; }
    }
}
