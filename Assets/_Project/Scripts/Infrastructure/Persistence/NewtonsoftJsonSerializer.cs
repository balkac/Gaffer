using Gaffer.Application.Serialization;
using Gaffer.Common;
using Newtonsoft.Json;

namespace Gaffer.Infrastructure.Persistence
{
    /// <summary>
    /// The JSON adapter for <see cref="ISerializer"/> (TDD §10): Newtonsoft turns a save payload into text
    /// and back. Serialize is total; Deserialize wraps the parse in a <see cref="Result{T}"/> because a
    /// corrupt or foreign string is an expected failure at the adapter boundary (CONVENTIONS §4), not an
    /// exception the pure core should ever see. The payload is plain DTOs (Application/Serialization), so the
    /// same round-trip is verified headless in the test bridge against this exact library.
    /// </summary>
    public sealed class NewtonsoftJsonSerializer : ISerializer
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };

        public string Serialize(SeasonSaveData data)
        {
            return JsonConvert.SerializeObject(data, Settings);
        }

        public Result<SeasonSaveData> Deserialize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Result<SeasonSaveData>.Failure("Save text is empty.");
            }

            try
            {
                var data = JsonConvert.DeserializeObject<SeasonSaveData>(text);
                if (data == null)
                {
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
