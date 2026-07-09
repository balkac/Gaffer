using System.IO;
using Gaffer.Application.Serialization;
using Gaffer.Common;

namespace Gaffer.Infrastructure.Persistence
{
    /// <summary>
    /// Reads and writes a save payload to a file. Save serializes and writes; Load reads, deserializes, and
    /// migrates to the current schema — each fallible step returns a <see cref="Result"/>, so a missing or
    /// corrupt file is an expected failure the caller handles, never a crash. Synchronous: a single small
    /// save file for the run. The async I/O boundary (TDD §10) matters once saves grow or move off the main
    /// thread; this keeps the adapter honest and the editor demo simple until then.
    /// </summary>
    public sealed class JsonSaveStore
    {
        private readonly ISerializer _serializer;
        private readonly SaveMigrator _migrator;

        public JsonSaveStore(ISerializer serializer, SaveMigrator migrator)
        {
            _serializer = serializer;
            _migrator = migrator;
        }

        public Result Save(string path, SeasonSaveData data)
        {
            try
            {
                File.WriteAllText(path, _serializer.Serialize(data));
                return Result.Success();
            }
            catch (IOException e)
            {
                return Result.Failure("Could not write save: " + e.Message);
            }
        }

        public Result<SeasonSaveData> Load(string path)
        {
            if (!File.Exists(path))
            {
                return Result<SeasonSaveData>.Failure("No save at " + path + ".");
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException e)
            {
                return Result<SeasonSaveData>.Failure("Could not read save: " + e.Message);
            }

            Result<SeasonSaveData> parsed = _serializer.Deserialize(text);
            if (parsed.IsFailure)
            {
                return parsed;
            }

            // Bring an older save up to the current schema before the caller maps it back to the domain.
            return _migrator.Migrate(parsed.Value);
        }
    }
}
