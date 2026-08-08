using System;
using System.IO;
using Gaffer.Application.Serialization;
using Gaffer.Common;

namespace Gaffer.UserData
{
    /// <summary>
    /// The save codec the game wires: it WRITES the compact binary container
    /// (<see cref="BinarySaveSerializer"/>) and READS either that or the legacy indented JSON
    /// (<see cref="NewtonsoftJsonSerializer"/>) that schema v5 and earlier shipped as.
    ///
    /// <para>
    /// MIGRATION DECISION — accept both on load, write only the new one. The alternative was a one-shot
    /// conversion pass at boot, and it was rejected for three reasons. It needs a moment to run that nobody
    /// owns (the app is being opened, possibly offline, possibly about to be backgrounded), and a
    /// conversion is a save — so it lands in the middle of the one protocol that must not be interrupted.
    /// It has to be written, tested, and then carried forever anyway, because a player restoring an old
    /// device backup produces a v5 JSON file long after the conversion "ran". And it buys nothing that this
    /// does not: the FIRST save after the update rewrites the file in the new format through the ordinary
    /// atomic path, and the <c>.bak</c> the replace leaves behind is the old JSON — a real fallback rather
    /// than a converted one. The cost is that the JSON reader stays in the build; it is small, already
    /// tested, and doubles as the readable format for a save pasted into a bug report.
    /// </para>
    ///
    /// <para>
    /// DETECTION is by the four magic bytes at the head of a binary save, not by file name or extension: the
    /// same path holds both formats through the transition, and a save's name is not evidence of its
    /// content. Anything that is not the magic is handed to the JSON reader from byte zero, which is also
    /// what makes a file that is neither fail with JSON's message rather than a confusing binary one.
    /// </para>
    /// </summary>
    public sealed class SaveSerializer : ISerializer
    {
        private readonly BinarySaveSerializer _binary;
        private readonly ISerializer _legacyText;

        /// <summary>The shipped wiring: binary out, binary-or-legacy-JSON in.</summary>
        public SaveSerializer()
            : this(new BinarySaveSerializer(), new NewtonsoftJsonSerializer())
        {
        }

        /// <summary>Explicit wiring, for tests that need to see which codec was reached.</summary>
        public SaveSerializer(BinarySaveSerializer binary, ISerializer legacyText)
        {
            _binary = binary ?? throw new ArgumentNullException(nameof(binary));
            _legacyText = legacyText ?? throw new ArgumentNullException(nameof(legacyText));
        }

        public void Serialize(SeasonSaveData data, Stream stream)
        {
            _binary.Serialize(data, stream);
        }

        /// <summary>
        /// Sniffs the first four bytes and dispatches. The stream must be seekable — a save is a file, and
        /// requiring the seek is cheaper and clearer than a lookahead wrapper that would exist for a caller
        /// that does not exist. A non-seekable stream is our own misuse, so it throws rather than returning
        /// a Result: CONVENTIONS §4 keeps Result for the file's problems, not for the caller's.
        /// </summary>
        public Result<SeasonSaveData> Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            if (!stream.CanSeek)
            {
                throw new ArgumentException("The save stream must be seekable so the format can be detected.", nameof(stream));
            }

            long start = stream.Position;
            bool isBinary = LooksLikeBinary(stream);
            stream.Position = start;

            return isBinary ? _binary.Deserialize(stream) : _legacyText.Deserialize(stream);
        }

        /// <summary>True when the stream starts with the binary container's magic. A file shorter than the
        /// magic is not binary — it falls through to the JSON reader, which reports it as unparsable.</summary>
        private static bool LooksLikeBinary(Stream stream)
        {
            for (int i = 0; i < SaveBinaryPrimitives.Magic.Length; i++)
            {
                int b = stream.ReadByte();
                if (b != SaveBinaryPrimitives.Magic[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
