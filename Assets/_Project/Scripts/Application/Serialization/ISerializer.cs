using System.IO;
using Gaffer.Common;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Turns a save payload into text and back. The port lives here (pure); the JSON implementation and file
    /// I/O live in Infrastructure (TDD §10). Deserialize returns a Result because parsing a corrupt or
    /// foreign string is an expected failure, caught at the adapter boundary (CONVENTIONS §4).
    /// <para>
    /// The port is expressed over <see cref="TextWriter"/>/<see cref="TextReader"/> rather than
    /// <c>string</c> so the adapter can stream straight to and from the file instead of materialising the
    /// whole save — a full league's squads is a multi-hundred-KB string, and on a mobile heap that is a
    /// large-object allocation per save for no reason (PERFORMANCE §4). Both are plain BCL abstractions, so
    /// the port stays framework-free; a caller that genuinely wants a string wraps a
    /// <c>StringWriter</c>/<c>StringReader</c>.
    /// </para>
    /// </summary>
    public interface ISerializer
    {
        /// <summary>Writes the payload as text. Total: the payload is plain DTOs the adapter is expected to
        /// handle, so a failure here is a bug, not an outcome.</summary>
        void Serialize(SeasonSaveData data, TextWriter writer);

        /// <summary>Reads a payload back. Empty, corrupt, or foreign text is an expected failure.</summary>
        Result<SeasonSaveData> Deserialize(TextReader reader);
    }
}
