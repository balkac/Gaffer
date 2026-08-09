using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// The run's own state in DOMAIN types — the shape <c>RunSession</c> hands out when it is captured and
    /// takes back when it is resumed, with <see cref="SeasonSaveMapper"/> the only thing between it and
    /// <see cref="RunSaveData"/>.
    /// <para>
    /// ONE TYPE FOR BOTH DIRECTIONS, deliberately. Capture and restore carry exactly the same facts, and a
    /// pair of near-identical types is a pair that drifts: a field added to one and forgotten on the other
    /// is a field that saves and never loads, which is precisely the class of bug this whole change exists
    /// to fix. The compiler cannot catch that; sharing the type means there is nothing to catch.
    /// </para>
    /// <para>
    /// Every member is optional in the same sense <see cref="RunSaveData"/>'s groups are, and carries the
    /// same meaning: null (or an absent nullable) means "this save does not know", and the run falls back
    /// to its setup or starts that piece fresh. The mapper does not invent values for a document that
    /// lacks them — the fallbacks live in one place, <c>RunSession</c>'s constructor, so there is one
    /// answer to "what does a save without a market do" rather than two that can disagree.
    /// </para>
    /// </summary>
    public sealed class RunState
    {
        public RunState(
            ulong originalSeed,
            RunSetupState setup,
            Finances? finances,
            Formation? formation,
            Tactics? tactics,
            IReadOnlyList<int> eleven,
            IReadOnlyList<Player> market,
            IReadOnlyList<MoraleEntry> morale,
            DramaEngineState drama,
            DramaEventId pendingEvent,
            int pendingSubjectPlayerId)
        {
            OriginalSeed = originalSeed;
            Setup = setup;
            Finances = finances;
            Formation = formation;
            Tactics = tactics;
            Eleven = eleven;
            Market = market;
            Morale = morale;
            Drama = drama;
            PendingEvent = pendingEvent;
            PendingSubjectPlayerId = pendingSubjectPlayerId;
        }

        /// <summary>The seed the world was generated from. Survives every resume, however many fresh
        /// continuation seeds the run has been played on since.</summary>
        public ulong OriginalSeed { get; }

        public RunSetupState Setup { get; }

        public Finances? Finances { get; }

        public Formation? Formation { get; }

        public Tactics? Tactics { get; }

        /// <summary>One player id per formation slot, <see cref="RunSaveData.NoPlayer"/> for an empty one.</summary>
        public IReadOnlyList<int> Eleven { get; }

        public IReadOnlyList<Player> Market { get; }

        public IReadOnlyList<MoraleEntry> Morale { get; }

        public DramaEngineState Drama { get; }

        /// <summary>The unanswered event, or the default id when the week is clear.</summary>
        public DramaEventId PendingEvent { get; }

        public int PendingSubjectPlayerId { get; }
    }

    /// <summary>
    /// The persisted slice of <c>RunSetup</c> — the knobs that describe THIS run rather than the next one
    /// somebody starts. It is a separate type from <c>RunSetup</c> on purpose: half of that type (the
    /// seed, the money, the shape, the stakes) is either recorded elsewhere in the save or is not run
    /// state at all, and a resume that quietly re-applied all of it would be the very
    /// re-seed-from-the-window bug being fixed.
    /// </summary>
    public sealed class RunSetupState
    {
        public RunSetupState(
            int managedClubIndex,
            int teamCount,
            int promotionPosition,
            int survivalPosition,
            int marketSize,
            int guaranteedGems)
        {
            ManagedClubIndex = managedClubIndex;
            TeamCount = teamCount;
            PromotionPosition = promotionPosition;
            SurvivalPosition = survivalPosition;
            MarketSize = marketSize;
            GuaranteedGems = guaranteedGems;
        }

        public int ManagedClubIndex { get; }

        public int TeamCount { get; }

        public int PromotionPosition { get; }

        public int SurvivalPosition { get; }

        public int MarketSize { get; }

        public int GuaranteedGems { get; }
    }
}
