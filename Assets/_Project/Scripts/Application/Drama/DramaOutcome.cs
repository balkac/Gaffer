using System;
using System.Collections.Generic;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// One morale consequence a drama choice carries: points on a player for a number of weeks. It is
    /// *described*, not applied — the resolver no longer writes to a ledger behind the caller's back, so
    /// the outcome record really does describe everything that changed and a UI can replay it
    /// (ARCHITECTURE §8). <see cref="Gaffer.Application.Run.RunSession"/> is the single owner that
    /// applies it, together with the cash and the sale, in one ordered step (§8a).
    /// </summary>
    public readonly struct MoraleChange
    {
        public MoraleChange(PlayerId player, double points, int weeks)
        {
            Player = player;
            Points = points;
            Weeks = weeks;
        }

        public PlayerId Player { get; }

        /// <summary>Signed morale points — a wound is negative, a lift positive.</summary>
        public double Points { get; }

        /// <summary>How many weeks the entry stays live before it fades.</summary>
        public int Weeks { get; }
    }

    /// <summary>
    /// What resolving a drama choice does — <em>everything</em> it does, as data: the morale entries to
    /// land on the ledger, the signed cash consequence, a forced sale, and a trait passed to a teammate.
    /// Nothing is applied here. Command in, outcome out (ARCHITECTURE §8, CLAUDE.md NON-NEGOTIABLE #4):
    /// the resolver decides, and one owner applies the whole thing in order, so a step that cannot be
    /// carried out (a sale the squad size forbids) rejects the resolution before any of it lands rather
    /// than leaving a half-committed transaction behind.
    /// </summary>
    public sealed class DramaOutcome
    {
        private static readonly IReadOnlyList<MoraleChange> NoMoraleChanges = Array.Empty<MoraleChange>();

        public DramaOutcome(DramaEventId eventId, int choiceIndex, long cashDelta, Player playerToSell, Player traitGrantTarget = null, TraitId grantedTrait = default)
            : this(eventId, choiceIndex, cashDelta, playerToSell, NoMoraleChanges, traitGrantTarget, grantedTrait)
        {
        }

        public DramaOutcome(
            DramaEventId eventId,
            int choiceIndex,
            long cashDelta,
            Player playerToSell,
            IReadOnlyList<MoraleChange> moraleChanges,
            Player traitGrantTarget = null,
            TraitId grantedTrait = default)
        {
            EventId = eventId;
            ChoiceIndex = choiceIndex;
            CashDelta = cashDelta;
            PlayerToSell = playerToSell;
            MoraleChanges = moraleChanges ?? NoMoraleChanges;
            TraitGrantTarget = traitGrantTarget;
            GrantedTrait = grantedTrait;
        }

        public DramaEventId EventId { get; }

        public int ChoiceIndex { get; }

        /// <summary>Signed cash consequence for the club's finances (fines in, sweeteners and cuts out).</summary>
        public long CashDelta { get; }

        /// <summary>Set when the choice sells the subject; the caller executes the transfer it owns.</summary>
        public Player PlayerToSell { get; }

        /// <summary>
        /// The morale entries this choice produces, in the order the effects were authored — one per
        /// affected player (a team-wide effect yields one per squad member). Applying them to a
        /// <see cref="MoraleLedger"/> in this order is exactly what the engine used to do in place.
        /// </summary>
        public IReadOnlyList<MoraleChange> MoraleChanges { get; }

        /// <summary>Set when a trait passes to a teammate (the anointed successor); the caller rebuilds him.</summary>
        public Player TraitGrantTarget { get; }

        /// <summary>The trait <see cref="TraitGrantTarget"/> receives.</summary>
        public TraitId GrantedTrait { get; }
    }
}
