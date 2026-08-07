using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Brings a loaded save up to the current schema, and is the gate every load passes through. It owns the
    /// ACCEPTANCE POLICY — which versions this build will read — while the schema a document claims is a fact
    /// about the document and lives on <see cref="SeasonSaveData.CurrentVersion"/> (ARCHITECTURE §11). The
    /// policy here is the simple one that is correct while saves are only ever read by the build that wrote
    /// them: accept anything at or below the current version, reject anything above it.
    /// <para>
    /// Migration is a CHAIN: each step lifts a document one version and is written so it can only ever read
    /// the fields the older schema actually had. Steps run in ascending order and every one of them has a
    /// test that starts from a genuine payload of the previous version (UNITY.md §7: never ship a schema
    /// change without a migration test from the previous version).
    /// </para>
    /// </summary>
    public sealed class SaveMigrator
    {
        public Result<SeasonSaveData> Migrate(SeasonSaveData data)
        {
            // Acceptance policy, not a document fact: a save written by a newer build may contain shapes this
            // one cannot express, so it is rejected rather than half-read.
            if (data.SchemaVersion > SeasonSaveData.CurrentVersion)
            {
                return Result<SeasonSaveData>.Failure(
                    $"Save schema {data.SchemaVersion} is newer than the supported version {SeasonSaveData.CurrentVersion}.");
            }

            // The chain. One `if` per step, ascending, each guarded by the version it lifts FROM — so a v2
            // document falls through every step in order and a current one runs none of them.
            if (data.SchemaVersion < 5)
            {
                Result step = MigrateToV5(data);
                if (step.IsFailure)
                {
                    return Result<SeasonSaveData>.Failure(step.Error);
                }
            }

            // Validation of the current shape, after the chain: a v5 document written by this build is only
            // as trustworthy as the file it came from, and a save is a file on a device a player can edit.
            Result roles = ValidateRoleNames(data);
            if (roles.IsFailure)
            {
                return Result<SeasonSaveData>.Failure(roles.Error);
            }

            data.SchemaVersion = SeasonSaveData.CurrentVersion;
            return Result<SeasonSaveData>.Success(data);
        }

        /// <summary>
        /// v4 → v5: a player's role moves from the raw <see cref="PlayerRole"/> ordinal to the member NAME.
        /// The old ordinal is still readable only because <see cref="PlayerRole"/>'s values are pinned; an
        /// ordinal outside the defined set means the document is corrupt and fails the load rather than
        /// resolving to whichever role happens to sit at that number. The legacy field is cleared as it is
        /// converted, so the migrated document no longer carries the retired member.
        /// </summary>
        private static Result MigrateToV5(SeasonSaveData data)
        {
            foreach (ClubSaveData club in data.Clubs)
            {
                // A v2 save (or a strength-only harness fixture) has no roster at all — nothing to convert.
                if (club.Squad == null)
                {
                    continue;
                }

                foreach (PlayerSaveData player in club.Squad)
                {
                    // Already named: a document that reached v5 by another path (or a partially-migrated
                    // one) must not be reprocessed — migration steps are idempotent by construction.
                    if (!string.IsNullOrEmpty(player.RoleName))
                    {
                        player.Role = null;
                        continue;
                    }

                    if (!player.Role.HasValue)
                    {
                        return Result.Failure(
                            $"Save is missing the role of player {player.Id} ('{player.Name}') in club {club.Id}.");
                    }

                    if (!PersistedPlayerRole.TryFromLegacyOrdinal(player.Role.Value, out PlayerRole role))
                    {
                        return Result.Failure(
                            $"Save has an unknown role ordinal {player.Role.Value} for player {player.Id} ('{player.Name}').");
                    }

                    player.RoleName = PersistedPlayerRole.ToName(role);
                    player.Role = null;
                }
            }

            return Result.Success();
        }

        /// <summary>Every role name in the document must resolve to a defined member. An unparseable one is an
        /// expected failure of the load (the file is corrupt, or was written by a build that knew a role this
        /// one does not), surfaced as a <see cref="Result"/> so the caller can fall back — never a silent
        /// default that would hand the player a squad of goalkeepers (CONVENTIONS §4, §6).</summary>
        private static Result ValidateRoleNames(SeasonSaveData data)
        {
            foreach (ClubSaveData club in data.Clubs)
            {
                if (club.Squad == null)
                {
                    continue;
                }

                foreach (PlayerSaveData player in club.Squad)
                {
                    if (!PersistedPlayerRole.TryParse(player.RoleName, out PlayerRole _))
                    {
                        return Result.Failure(
                            $"Save has an unknown role '{player.RoleName}' for player {player.Id} ('{player.Name}').");
                    }
                }
            }

            return Result.Success();
        }
    }
}
