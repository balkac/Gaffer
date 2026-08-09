using System.IO;
using Gaffer.Common;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Turns a save payload into bytes and back. The port lives here (pure); the codecs and the file I/O
    /// live in the <c>Gaffer.UserData</c> adapter assembly (TDD §10). Deserialize returns a Result because
    /// parsing a corrupt or foreign save is an expected failure, caught at the adapter boundary
    /// (CONVENTIONS §4).
    /// <para>
    /// The port is expressed over <see cref="Stream"/> — a plain BCL abstraction, so it stays framework-free
    /// — and not over <c>string</c>, so the adapter can stream straight to and from the file instead of
    /// materialising the whole save. That mattered when the save was a multi-hundred-KB string
    /// (PERFORMANCE §4); at the design target of a ~50,000-player world it is the difference between a few
    /// KB of buffers and a multi-MB allocation on a non-compacting heap (PERFORMANCE §10).
    /// </para>
    /// <para>
    /// It was <see cref="TextWriter"/>/<see cref="TextReader"/> until the save format became binary
    /// (<c>BinarySaveSerializer</c>): a byte-oriented codec cannot be expressed over a text port without
    /// an encoding round trip that would defeat the point. A codec that IS text — the legacy JSON reader —
    /// wraps the stream in a <c>StreamReader</c>/<c>StreamWriter</c> itself, which also puts the encoding
    /// choice where it belongs, inside the codec that has an opinion about it, rather than in the file store.
    /// </para>
    /// </summary>
    public interface ISerializer
    {
        /// <summary>Writes the payload. Total: the payload is plain DTOs the adapter is expected to handle,
        /// so a failure here is a bug, not an outcome. The stream is left open — the caller owns it, and
        /// needs it to flush the file to disk itself.</summary>
        void Serialize(SeasonSaveData data, Stream stream);

        /// <summary>Reads a payload back. Empty, truncated, corrupt, or foreign bytes are expected failures.
        /// The stream is left open.</summary>
        Result<SeasonSaveData> Deserialize(Stream stream);
    }
}
