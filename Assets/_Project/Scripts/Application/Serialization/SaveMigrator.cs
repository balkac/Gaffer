using Gaffer.Common;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Brings a loaded save up to the current schema. A save from a newer version is rejected; an
    /// older one is migrated step by step (none defined yet at version 1). This is where schema
    /// changes stay backward-compatible.
    /// </summary>
    public sealed class SaveMigrator
    {
        public Result<SeasonSaveData> Migrate(SeasonSaveData data)
        {
            if (data.SchemaVersion > SaveSchema.CurrentVersion)
            {
                return Result<SeasonSaveData>.Failure(
                    $"Save schema {data.SchemaVersion} is newer than the supported version {SaveSchema.CurrentVersion}.");
            }

            // Future migrations run here while data.SchemaVersion < SaveSchema.CurrentVersion.

            data.SchemaVersion = SaveSchema.CurrentVersion;
            return Result<SeasonSaveData>.Success(data);
        }
    }
}
