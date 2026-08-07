using System;
using System.IO;
using Gaffer.Application.Serialization;
using Gaffer.Common;
using Newtonsoft.Json;

namespace Gaffer.Infrastructure.Persistence
{
    /// <summary>
    /// The JSON adapter for <see cref="ISerializer"/> (TDD §10): Newtonsoft turns a save payload into text
    /// and back. Serialize is total; Deserialize wraps the parse in a <see cref="Result{T}"/> because a
    /// corrupt or foreign string is an expected failure at the adapter boundary (CONVENTIONS §4), not an
    /// exception the pure core should ever see. The payload is plain DTOs (Application/Serialization).
    /// <para>
    /// It streams through the caller's reader/writer rather than building the save as one string
    /// (PERFORMANCE §4) and reuses one cached <see cref="JsonSerializer"/> — building it per call re-reads
    /// the settings and rebuilds the contract resolver's type cache every save. The cached instance is
    /// shared, which is safe here because <see cref="JsonSaveStore"/> is synchronous and saves happen on the
    /// caller's thread; a future async save path must not hand this one instance to two threads at once.
    /// </para>
    /// </summary>
    public sealed class NewtonsoftJsonSerializer : ISerializer
    {
        /// <summary>
        /// STRICTNESS POSTURE (ARCHITECTURE §11) — stated, not defaulted, because this is the setting that
        /// has to flip if the delivery model ever changes, and nothing else will remind anyone.
        /// <para>
        /// This serializer reads PLAYER SAVE DATA, which outlives the build that wrote it, so it is
        /// deliberately TOLERANT: <see cref="MissingMemberHandling.Ignore"/> means a member this build does
        /// not know is skipped instead of failing the load, and a member a newer field expects but an older
        /// document lacks takes its default. That is what makes "adding a field" back-compatible; a shape
        /// change that needs more than a default is handled by a step in <see cref="SaveMigrator"/>
        /// (UNITY.md §7 — migration belongs to player data).
        /// </para>
        /// <para>
        /// CONTENT takes the OPPOSITE posture and must not be read through this: the drama/trait
        /// <c>.asset</c> files ship inside the build, so file and reader are atomic and there is no
        /// acceptance policy to tune — a misspelled or unknown member there is an authoring typo that should
        /// break CI, not something to tolerate. The day content stops travelling with the binary, that
        /// choice inverts (ARCHITECTURE §11) and this comment is the marker for it.
        /// </para>
        /// </summary>
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            MissingMemberHandling = MissingMemberHandling.Ignore,
        };

        private static readonly JsonSerializer Serializer = JsonSerializer.Create(Settings);

        public void Serialize(SeasonSaveData data, TextWriter writer)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // CloseOutput stays false: the caller owns the writer (and the FileStream under it), so the
            // store can flush the stream to disk itself before the replace step.
            var jsonWriter = new JsonTextWriter(writer) { CloseOutput = false };
            Serializer.Serialize(jsonWriter, data);
            jsonWriter.Flush();
        }

        public Result<SeasonSaveData> Deserialize(TextReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            try
            {
                SeasonSaveData data;
                using (var jsonReader = new JsonTextReader(reader) { CloseInput = false })
                {
                    data = Serializer.Deserialize<SeasonSaveData>(jsonReader);
                }

                if (data == null)
                {
                    // Empty or whitespace-only input lands here: valid JSON for "nothing", not a save.
                    return Result<SeasonSaveData>.Failure("Save text did not parse into a save payload.");
                }

                return Result<SeasonSaveData>.Success(data);
            }
            catch (JsonException e)
            {
                return Result<SeasonSaveData>.Failure("Could not parse save: " + e.Message);
            }
        }
    }
}
