using System.Collections.Generic;

namespace Gaffer.Application.Serialization
{
    // One grouped serialization payload (CONVENTIONS §2 DTO exception). Mutable POCOs so any JSON
    // serializer can round-trip them; the domain stays immutable and maps to and from these.

    public sealed class SeasonSaveData
    {
        /// <summary>
        /// Which schema THIS class implements — a fact about the document, so it lives on the document type
        /// beside the field it describes rather than in a shared version holder (ARCHITECTURE §11: a version
        /// is two different facts, and a generic constants bucket separates a version from the fields it
        /// describes). The other fact — which versions this build ACCEPTS — is a policy and lives in
        /// <see cref="SaveMigrator"/>, which rejects a newer document and migrates an older one.
        /// <para>
        /// STRICTNESS POSTURE (ARCHITECTURE §11), stated rather than defaulted, because it is what has to
        /// flip if the delivery model ever changes: this is PLAYER SAVE DATA, which outlives the build, so
        /// the reader is TOLERANT — an unknown member is ignored and a missing member takes its default,
        /// and a shape change is handled by a migration step here (never by failing the parse). Content that
        /// ships INSIDE the build (the drama/trait `.asset` files, <c>Infrastructure/Configuration</c>) takes
        /// the opposite posture: file and reader are atomic, so an unknown or misspelled member is a
        /// CI-breaking error rather than something to tolerate. The tolerant setting is applied explicitly in
        /// <c>NewtonsoftJsonSerializer</c>.
        /// </para>
        /// <para>
        /// Version history:
        /// v2 — matches are seeded per fixture from a fixed season seed, so the save stores that seed
        /// (<see cref="MatchSeed"/>) instead of an evolving rng state.
        /// v3 — full squads and the season number are persisted, so a multi-season run survives development
        /// and renewal. A v2 save has no squads (clubs restore as strength only) and no season number.
        /// v4 — each player persists his trait ids (slugs), so the character layer survives a reload. A v3
        /// player has no traits field and restores trait-less.
        /// v5 — a player's role is persisted by NAME (<see cref="PlayerSaveData.RoleName"/>) instead of the
        /// enum ordinal, so inserting a role can no longer silently re-role every saved player
        /// (CONVENTIONS §6, UNITY.md §7). A v4 save's <see cref="PlayerSaveData.Role"/> ordinal is converted
        /// to the name by the migration step.
        /// v6 — the document carries the whole RUN, not just the season: finances, the managed club's
        /// tactics, shape and chosen eleven, live morale, the drama engine's memory and any unanswered
        /// event, the market, and the run's own setup (<see cref="Run"/>). Everything in that block used to
        /// be re-seeded from <c>RunSetup</c> — an editor window's input fields — on every load, which is
        /// why loading a save meant re-entering the cash and the wage budget and losing the team sheet. A
        /// v5 save has no run block; the migration step gives it one carrying the only run fact a v5
        /// document holds (its seed) and leaves every other group absent, so a v5 save resumes exactly as
        /// it did before — from the caller's setup.
        /// </para>
        /// </summary>
        /// <remarks>A <c>const</c> is baked into the calling assembly (CONVENTIONS §6), which normally argues
        /// for <c>static readonly</c> across an assembly boundary. It is safe here because every consumer is
        /// compiled from source in the same pass (Unity asmdefs and the dotnet test bridge alike) — there is
        /// no pre-built consumer that could hold a stale copy.</remarks>
        public const int CurrentVersion = 7;

        /// <summary>The schema this document was written with. <see cref="SaveMigrator"/> stamps it to
        /// <see cref="CurrentVersion"/> once the migration chain has run.</summary>
        public int SchemaVersion { get; set; }

        public string LeagueName { get; set; }

        /// <summary>Which season of the run this is — so a save resumes into the right year (multi-season runs).</summary>
        public int SeasonNumber { get; set; }

        public List<ClubSaveData> Clubs { get; set; } = new List<ClubSaveData>();

        public int PlayedRounds { get; set; }

        public List<MatchResultSaveData> Results { get; set; } = new List<MatchResultSaveData>();

        /// <summary>
        /// The fixed season seed each match is derived from — enough to reproduce every REMAINING fixture.
        /// <para>
        /// Its meaning is unchanged by v6 and that is worth stating, because what moves it did change: a
        /// resume now takes a fresh continuation seed from its caller and plays on that, so the next save
        /// writes the new seed here. It is still "the seed the unplayed fixtures come from"; it is simply
        /// no longer the same number for the whole run. The number the run was BORN on lives on
        /// <see cref="RunSaveData.OriginalSeed"/>.
        /// </para>
        /// </summary>
        public ulong MatchSeed { get; set; }

        /// <summary>
        /// The run around the season (v6): money, tactics, the eleven, morale, drama and the market. Null
        /// on a pre-v6 document and legal to be null afterwards — a capture with no run in hand (a harness
        /// snapshot of a league) writes the block with only the seed in it. Every group inside states what
        /// its own absence means; nothing here is required for a save to load.
        /// </summary>
        public RunSaveData Run { get; set; }
    }

    public sealed class ClubSaveData
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public double Attack { get; set; }

        public double Midfield { get; set; }

        public double Defence { get; set; }

        /// <summary>The full roster, so development and renewal survive across seasons. Null for a
        /// strength-only club (a v2 save, or a harness fixture built from strength alone).</summary>
        public List<PlayerSaveData> Squad { get; set; }
    }

    public sealed class PlayerSaveData
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Nationality { get; set; }

        /// <summary>The specific <c>PlayerRole</c> by NAME (v5+), so the broad position and ratings rebuild
        /// from it. A name survives reordering the enum; the ordinal it replaced did not (CONVENTIONS §6,
        /// UNITY.md §7). The name is now wire format: renaming a <c>PlayerRole</c> member is a breaking
        /// change that needs its own migration step. Read it through <see cref="PersistedPlayerRole"/>,
        /// never with a bare <c>Enum.Parse</c>.</summary>
        public string RoleName { get; set; }

        /// <summary>LEGACY (v4 and older): the role as a raw <c>PlayerRole</c> ordinal. Nullable and written
        /// as <c>null</c> from v5 on, so the serializer's null handling drops it from new documents; it
        /// exists only so <see cref="SaveMigrator"/> can read an old save and fill <see cref="RoleName"/>.
        /// This is the pattern for every future field a migration has to retire: keep the old member on the
        /// DTO, make it nullable, and clear it in the step that converts it — never delete the member the
        /// old documents on players' devices still carry.</summary>
        public int? Role { get; set; }

        public int Age { get; set; }

        public int HiddenPotential { get; set; }

        public AttributesSaveData Attributes { get; set; }

        /// <summary>Trait id slugs (v4). Null on an older save — the player restores trait-less; ids the
        /// current catalog does not define are carried but ignored by the sim (config is not serialized,
        /// definitions rebind by id on load, TDD §10).</summary>
        public List<string> Traits { get; set; }
    }

    // The 29 grouped attributes, flat so any JSON serializer round-trips them by name (robust to reordering).
    public sealed class AttributesSaveData
    {
        public byte Finishing { get; set; }
        public byte Technique { get; set; }
        public byte FirstTouch { get; set; }
        public byte Dribbling { get; set; }
        public byte Passing { get; set; }
        public byte Crossing { get; set; }
        public byte Heading { get; set; }
        public byte LongShots { get; set; }
        public byte Marking { get; set; }
        public byte Tackling { get; set; }
        public byte Penalties { get; set; }
        public byte FreeKicks { get; set; }
        public byte Corners { get; set; }
        public byte LongThrows { get; set; }
        public byte Pace { get; set; }
        public byte Acceleration { get; set; }
        public byte Stamina { get; set; }
        public byte Strength { get; set; }
        public byte Agility { get; set; }
        public byte Jumping { get; set; }
        public byte Balance { get; set; }
        public byte Positioning { get; set; }
        public byte Reflexes { get; set; }
        public byte Handling { get; set; }
        public byte AerialReach { get; set; }
        public byte CommandOfArea { get; set; }
        public byte OneOnOnes { get; set; }
        public byte Kicking { get; set; }
        public byte GkPositioning { get; set; }
    }

    public sealed class MatchResultSaveData
    {
        public int Home { get; set; }

        public int Away { get; set; }

        public int HomeGoals { get; set; }

        public int AwayGoals { get; set; }
    }
}
