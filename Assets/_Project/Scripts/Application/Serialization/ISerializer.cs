using Gaffer.Common;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Turns a save payload into text and back. The port lives here (pure); the JSON implementation
    /// and file I/O live in Infrastructure (TDD §10). Deserialize returns a Result because parsing a
    /// corrupt or foreign string is an expected failure, caught at the adapter boundary (CONVENTIONS §4).
    /// </summary>
    public interface ISerializer
    {
        string Serialize(SeasonSaveData data);

        Result<SeasonSaveData> Deserialize(string text);
    }
}
