using System.Collections.Generic;

namespace Gaffer.Application.Serialization
{
    // The v6 run block, as one grouped serialization payload (CONVENTIONS §2 DTO exception). Mutable
    // POCOs for the same reason the season payload is: any serializer has to be able to round-trip them,
    // while the run itself stays immutable and maps to and from these.
    //
    // EVERY GROUP IS INDEPENDENTLY OPTIONAL, and that is the design, not laziness. A save outlives the
    // build that wrote it (ARCHITECTURE §11), so each group states what its own absence MEANS — and the
    // meanings were chosen so that "absent" reproduces exactly what the game did before v6 existed:
    // fall back to the caller's setup, or start that piece of the run fresh. That is what lets the v5
    // migration be a real step with no invented data (see SaveMigrator.MigrateToV6) instead of a table of
    // guesses about a run nobody recorded.

    /// <summary>
    /// The half of a run the season document never carried: the money, the manager's own tactical
    /// decisions, the market he is shopping in, the morale his choices left behind, and the drama engine's
    /// memory. Before v6 all of it was re-seeded from <c>RunSetup</c> on load — i.e. from an editor
    /// window's input fields — so loading a save meant re-entering the market size, the cash and the wage
    /// budget, and losing the tactics and the chosen eleven. This block is what makes a save the state of
    /// a RUN rather than a photograph of a season.
    /// </summary>
    public sealed class RunSaveData
    {
        /// <summary>
        /// The seed the run was GENERATED from — the one that reproduces this world from nothing. It is
        /// not the seed the remaining fixtures are played on: from v6 a resume takes a fresh continuation
        /// seed from its caller (<c>RunSessionFactory.Resume</c>), so <see cref="SeasonSaveData.MatchSeed"/>
        /// moves with each resume while this stays put. Kept because a reproducible run is worth having
        /// for a bug report even though the game does not replay one: hand this back as the continuation
        /// seed and the save resumes deterministically.
        /// </summary>
        public ulong OriginalSeed { get; set; }

        /// <summary>The run's own setup. Null on a save that predates v6 — the caller's setup stands, which
        /// is exactly what every load did before this block existed.</summary>
        public RunSetupSaveData Setup { get; set; }

        /// <summary>The club's money. Null re-seeds it from the setup's starting cash and budget and
        /// re-derives the wage bill from the restored squad.</summary>
        public FinancesSaveData Finances { get; set; }

        /// <summary>The managed club's shape and tactical setup. Null takes the setup's.</summary>
        public TacticsSaveData Tactics { get; set; }

        /// <summary>
        /// The chosen eleven as one player id per formation slot, in slot order;
        /// <see cref="NoPlayer"/> for a slot the manager left empty. Ids rather than a nested squad,
        /// because these players are already in the document — this is a team sheet, not a second roster.
        /// Null (or an id nobody in the squad carries) auto-picks that slot, so a save whose squad has
        /// changed underneath it degrades to the best available eleven instead of refusing to load.
        /// </summary>
        public List<int> Eleven { get; set; }

        /// <summary>This season's free agents. Null generates a fresh market — which is what a pre-v6 load
        /// did, and why the owner's shortlist used to vanish.</summary>
        public List<PlayerSaveData> Market { get; set; }

        /// <summary>Live morale, entry by entry with the weeks each has left. Null (or empty) is a quiet
        /// dressing room.</summary>
        public List<MoraleSaveData> Morale { get; set; }

        /// <summary>The drama engine's memory and the event still waiting on an answer. Null starts the
        /// engine fresh — no cooldowns, nothing fired, a clean season budget.</summary>
        public DramaSaveData Drama { get; set; }

        /// <summary>The <see cref="Eleven"/> entry for an empty slot. A player id is never negative (squad
        /// ids come from generation, market ids from a positive base), so this cannot collide with one.</summary>
        public const int NoPlayer = -1;
    }

    /// <summary>
    /// The knobs the run was started with that are still true of it — the ones an editor window otherwise
    /// has to re-supply on every load. Deliberately NOT the whole of <c>RunSetup</c>: the seed lives on
    /// <see cref="RunSaveData.OriginalSeed"/> and <see cref="SeasonSaveData.MatchSeed"/>, the money lives
    /// on <see cref="FinancesSaveData"/> (it has moved since kick-off), the shape and tactics on
    /// <see cref="TacticsSaveData"/> (the manager has changed them), and the match context is a run-wide
    /// stake setting rather than run state.
    /// </summary>
    public sealed class RunSetupSaveData
    {
        /// <summary>Which club the manager took, as the index the run actually settled on (already clamped
        /// into the league), not the raw setup number.</summary>
        public int ManagedClubIndex { get; set; }

        public int TeamCount { get; set; }

        public int PromotionPosition { get; set; }

        public int SurvivalPosition { get; set; }

        public int MarketSize { get; set; }

        public int GuaranteedGems { get; set; }
    }

    /// <summary>
    /// The two budgets and the bill against one of them. The wage ceiling is persisted rather than
    /// re-seeded because it is now player-modifiable (<c>RunSession.ShiftWageBudget</c>): re-seeding it
    /// from the setup would hand back a slice the manager sold for cash — and let him keep the cash.
    /// </summary>
    public sealed class FinancesSaveData
    {
        public long Cash { get; set; }

        public long WeeklyWageBudget { get; set; }

        /// <summary>What the squad already costs each week. Restored as written rather than re-derived:
        /// these are contracts the club signed, and the bill is re-derived from the roster at the next
        /// rollover anyway.</summary>
        public long WeeklyWageBill { get; set; }
    }

    /// <summary>
    /// The managed club's shape and tactical setup — the manager's own decisions, which is why they are
    /// saved rather than re-read from a window's dropdowns.
    /// <para>
    /// ONE CLUB, AND THAT IS THE WHOLE MODEL, not a shortcut. <c>LeagueSeason</c> does hold tactics, a
    /// formation and a team sheet per club, but only the managed club's are ever SET: every AI club falls
    /// through to the same defaults (4-4-2, balanced) and has its eleven auto-picked from its roster, which
    /// the restored squads reproduce exactly. So the managed club's is all there is to lose. The day an AI
    /// manager picks his own shape, this becomes a list and the migration that makes it one has a real
    /// difference to carry.
    /// </para>
    /// </summary>
    public sealed class TacticsSaveData
    {
        public string FormationName { get; set; }

        /// <summary>The shape as one <c>PlayerRole</c> NAME per slot, in slot order — the same by-name
        /// contract a player's own role travels under (NON-NEGOTIABLE #9). Storing the slots rather than
        /// only the preset's name is what lets a shape that is not one of the presets round-trip, and what
        /// stops a renamed preset from silently re-shaping a saved team sheet.</summary>
        public List<string> FormationSlots { get; set; }

        /// <summary>The four tactical axes by member NAME. Read them through <see cref="PersistedEnum"/>,
        /// never with a bare <c>Enum.Parse</c>.</summary>
        public string Mentality { get; set; }

        public string Tempo { get; set; }

        public string Pressing { get; set; }

        public string Approach { get; set; }
    }

    /// <summary>One live morale entry: signed points on a player with the weeks it still has to run. The
    /// ledger stacks entries and ages them a week per played round, so the remaining weeks — not the
    /// original duration — is what a resume has to carry.</summary>
    public sealed class MoraleSaveData
    {
        public int PlayerId { get; set; }

        public double Points { get; set; }

        public int WeeksLeft { get; set; }
    }

    /// <summary>
    /// The drama engine's memory: how far into the run it is, what it has fired and when, how much of the
    /// season's budget is spent — and the event still waiting on an answer, if one is open. Without this a
    /// reload handed the manager a fresh engine, which is a save-scum that costs nothing: reload, and the
    /// cooldowns, the once-per-run marks and the season budget were all back.
    /// </summary>
    public sealed class DramaSaveData
    {
        /// <summary>The engine's own week counter, which cooldowns and the minimum gap are measured in.</summary>
        public int Week { get; set; }

        /// <summary>The week ANY event last fired, for the minimum-gap rule.</summary>
        public int LastFiredWeek { get; set; }

        /// <summary>How much of this season's event budget is already spent.</summary>
        public int FiredThisSeason { get; set; }

        /// <summary>Every event that has fired at least once this run, with the week it last did — which is
        /// both the per-event cooldown and the once-per-run mark, because the engine sets them together.
        /// An id the current catalog no longer defines is carried and ignored (tolerant: config is not
        /// serialized, definitions rebind by id on load).</summary>
        public List<DramaEventSaveData> Events { get; set; }

        /// <summary>The event raised but not yet answered, by id; null when the week is clear. A blocked
        /// week that a reload silently cleared was a decision the manager got to skip.</summary>
        public string PendingEventId { get; set; }

        /// <summary>Who the pending event happened to, or <see cref="RunSaveData.NoPlayer"/> for a
        /// club-level one. The rest of the pending event's context (the room, the eleven, the table
        /// position, the streak, the window) is derived from the restored run rather than stored, because
        /// every part of it is already in the document.</summary>
        public int PendingSubjectPlayerId { get; set; }
    }

    /// <summary>One event's firing record.</summary>
    public sealed class DramaEventSaveData
    {
        public string Id { get; set; }

        public int LastFiredWeek { get; set; }
    }
}
