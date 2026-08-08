using System;
using System.IO;
using System.Text;
using Gaffer.Application.Serialization;
using Gaffer.Common;
using Newtonsoft.Json;

// ASSEMBLY NOTE — Gaffer.UserData (the pure save-data adapter assembly, ARCHITECTURE §1 / starter-tree
// "The optional pure-adapter assemblies"). Newtonsoft is a plain .NET library, so this adapter has no
// reason to live in the Unity-coupled Infrastructure assembly; here it is `noEngineReferences: true` and
// joins the `dotnet test` bridge, which is what lets SaveJsonRoundTripTests exercise the shipped
// JsonSerializerSettings headlessly.
//
// DELIBERATE DEVIATION from starter-tree's literal `UserData → Common` shape: that shape assumes the save
// DTOs live in this assembly too. Ours do not — `Application/Serialization` owns the ISerializer port, the
// wire DTOs and the DTO↔domain mapper, which is exactly what starter-tree's Application table row sanctions
// ("ISerializer port + wire DTOs + DTO→domain mapping (consumer-owned contract; pure)"), and SeasonSaveMapper
// needs Gaffer.Domain so it cannot follow the adapter down here without dragging Domain along. Referencing
// Gaffer.Application from this adapter is therefore the chosen compromise: the arrow still points inward,
// Application stays free of any third-party dependency (Newtonsoft is referenced here and nowhere in the
// core), and the DTOs keep a single owner. The rule this does NOT bend: nothing here may reference
// Gaffer.Content or any other adapter assembly (ARCHITECTURE §11 — content and saved data stay separate).
namespace Gaffer.UserData
{
    /// <summary>
    /// The JSON adapter for <see cref="ISerializer"/> (TDD §10): Newtonsoft turns a save payload into text
    /// and back. Serialize is total; Deserialize wraps the parse in a <see cref="Result{T}"/> because a
    /// corrupt or foreign string is an expected failure at the adapter boundary (CONVENTIONS §4), not an
    /// exception the pure core should ever see. The payload is plain DTOs (Application/Serialization).
    /// <para>
    /// IT IS NOW THE LEGACY READER. Schema v5 and earlier shipped as indented JSON, which cost 1,070 bytes
    /// a player — 51 MB for the design target of a ~50,000-player world — so the shipped codec is
    /// <see cref="BinarySaveSerializer"/> and this one stays to read the saves already on players' devices.
    /// <see cref="SaveSerializer"/> is what wires the two together; nothing in the game writes JSON any
    /// more. Do not delete it: those files are the migration path, and the same JSON is still the readable
    /// format for a save pasted into a bug report.
    /// </para>
    /// <para>
    /// It streams through the caller's stream rather than building the save as one string (PERFORMANCE §4)
    /// and reuses one cached <see cref="JsonSerializer"/> — building it per call re-reads the settings and
    /// rebuilds the contract resolver's type cache every save. The cached instance is shared, which is safe
    /// here because <see cref="JsonSaveStore"/> is synchronous and saves happen on the caller's thread; a
    /// future async save path must not hand this one instance to two threads at once.
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

        /// <summary>UTF-8 without a BOM. The encoding is the CODEC's business, not the file store's: a BOM is
        /// a byte every JSON reader has to be told about, and the save is machine-read only.</summary>
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        public void Serialize(SeasonSaveData data, Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            // leaveOpen / CloseOutput: the caller owns the stream (and the FileStream under it), so the
            // store can flush it to disk itself before the replace step.
            var writer = new StreamWriter(stream, Utf8NoBom, 1024, leaveOpen: true);
            var jsonWriter = new JsonTextWriter(writer) { CloseOutput = false };
            Serializer.Serialize(jsonWriter, data);
            jsonWriter.Flush();
            writer.Flush();
        }

        public Result<SeasonSaveData> Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            try
            {
                SeasonSaveData data;
                var reader = new StreamReader(stream, Utf8NoBom, detectEncodingFromByteOrderMarks: true, 1024, leaveOpen: true);
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
