using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gaffer.UserData
{
    /// <summary>
    /// The encoder half of the binary save container: the small set of primitives every record in
    /// <see cref="BinarySaveSerializer"/> is built from. <see cref="SaveBinaryReader"/> is its exact mirror,
    /// and the two are only ever correct together — a change here needs the same change there and a new
    /// container version.
    /// <para>
    /// EVERYTHING IS LITTLE-ENDIAN, stated rather than inherited: a save written on one device has to read
    /// on another, so the fixed-width writes compose their bytes by shifting rather than handing out the
    /// host's layout (no <c>BitConverter.GetBytes</c>, whose order follows the machine). Strings are UTF-8
    /// with no BOM. No number is ever formatted as text, which is what makes the format culture-proof by
    /// construction — there is no <c>double.ToString</c> to forget an invariant culture on, and a Turkish
    /// locale cannot change a single byte of the output (CONVENTIONS §6).
    /// </para>
    /// <para>
    /// It writes STRAIGHT THROUGH to the caller's stream and never buffers the document (PERFORMANCE §4):
    /// the whole point of the format is that a 50,000-player world never exists as one multi-megabyte
    /// buffer on a non-compacting heap (PERFORMANCE §10). The one buffer it does keep is a small reusable
    /// scratch array for UTF-8 encoding, so a per-player name does not allocate a fresh <c>byte[]</c>.
    /// </para>
    /// </summary>
    internal sealed class SaveBinaryWriter
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly Stream _stream;

        /// <summary>
        /// The file's string pool, built as the document is written rather than in a pre-pass. Low-cardinality
        /// text — nationalities, role names, trait slugs — repeats once per player across a whole world, so
        /// the first occurrence carries the bytes and every later one costs a single varint. Ordinal
        /// comparison because these are machine strings, not display text.
        /// </summary>
        private readonly Dictionary<string, int> _pool = new Dictionary<string, int>(StringComparer.Ordinal);

        private byte[] _scratch = new byte[256];

        internal SaveBinaryWriter(Stream stream)
        {
            _stream = stream;
        }

        internal void WriteMagicAndContainerVersion(ushort containerVersion)
        {
            _stream.Write(SaveBinaryPrimitives.Magic, 0, SaveBinaryPrimitives.Magic.Length);
            _stream.WriteByte((byte)containerVersion);
            _stream.WriteByte((byte)(containerVersion >> 8));
        }

        /// <summary>LEB128, the format's only variable-width integer: seven bits per byte, high bit set while
        /// more follow. A count or id below 128 costs one byte, which is what makes a player record ~55 bytes
        /// instead of ~1,070.</summary>
        internal void WriteVarUInt32(uint value)
        {
            while (value >= 0x80)
            {
                _stream.WriteByte((byte)(value | 0x80));
                value >>= 7;
            }

            _stream.WriteByte((byte)value);
        }

        /// <summary>
        /// An <c>int</c> as its unchecked <c>uint</c> reinterpretation. Deliberately NOT zigzag: every
        /// integer this format carries (ids, ages, potentials, goals, rounds) is non-negative in practice
        /// and would pay a second byte under zigzag from 64 upwards — a potential of 91 is one byte here and
        /// two under zigzag, across every player in the world. The reinterpretation still round-trips a
        /// negative exactly, it just spends five bytes doing it, which is the right way round for this data.
        /// </summary>
        internal void WriteInt32(int value)
        {
            WriteVarUInt32(unchecked((uint)value));
        }

        /// <summary>Fixed eight bytes, little-endian. Fixed rather than varint because this carries the match
        /// seed, which is uniformly random — a varint would average nine bytes for it.</summary>
        internal void WriteUInt64(ulong value)
        {
            for (int i = 0; i < 8; i++)
            {
                _stream.WriteByte((byte)(value >> (i * 8)));
            }
        }

        /// <summary>IEEE-754 bits, little-endian. Writing the BITS rather than a formatted number is what
        /// keeps a strength exact across a round trip and independent of the device's culture.</summary>
        internal void WriteDouble(double value)
        {
            WriteUInt64(unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
        }

        internal void WriteBytes(byte[] value, int offset, int count)
        {
            _stream.Write(value, offset, count);
        }

        /// <summary>
        /// A string that is expected to be unique — a league, club, or player name. Tag 0 means null;
        /// anything else is <c>byteLength + 1</c> followed by that many UTF-8 bytes, so an empty string
        /// (tag 1, no bytes) stays distinct from null.
        /// </summary>
        internal void WriteString(string value)
        {
            if (value == null)
            {
                WriteVarUInt32(SaveBinaryPrimitives.NullTag);
                return;
            }

            int count = Encode(value);
            WriteVarUInt32((uint)count + 1);
            _stream.Write(_scratch, 0, count);
        }

        /// <summary>
        /// A string drawn from a small set — a nationality, a persisted role NAME, a trait slug. The first
        /// occurrence writes the text; every later one writes its pool index. This is what keeps enums
        /// persisted BY NAME affordable: the file still contains the literal <c>"Goalkeeper"</c>, exactly
        /// once, and each goalkeeper costs one byte pointing at it — so the compact format buys its size
        /// back without reintroducing the raw ordinals v5 exists to retire (UNITY.md §7, CONVENTIONS §6).
        /// </summary>
        internal void WriteInternedString(string value)
        {
            if (value == null)
            {
                WriteVarUInt32(SaveBinaryPrimitives.NullTag);
                return;
            }

            if (_pool.TryGetValue(value, out int index))
            {
                WriteVarUInt32(SaveBinaryPrimitives.PoolBase + (uint)index);
                return;
            }

            _pool.Add(value, _pool.Count);
            WriteVarUInt32(SaveBinaryPrimitives.NewInternedTag);

            int count = Encode(value);
            WriteVarUInt32((uint)count);
            _stream.Write(_scratch, 0, count);
        }

        /// <summary>UTF-8-encodes into the reusable scratch buffer and returns the byte count. A string too
        /// long for the format is our own bug (the data cannot legitimately hold one), so it fails fast
        /// rather than writing a file the reader would reject — CONVENTIONS §4's other half.</summary>
        private int Encode(string value)
        {
            int maximum = Utf8.GetMaxByteCount(value.Length);
            if (maximum > _scratch.Length)
            {
                _scratch = new byte[maximum];
            }

            int count = Utf8.GetBytes(value, 0, value.Length, _scratch, 0);
            if (count > SaveBinaryPrimitives.MaxStringBytes)
            {
                throw new ArgumentException(
                    "A save string is " + count + " UTF-8 bytes, past the format's limit of "
                    + SaveBinaryPrimitives.MaxStringBytes + ".");
            }

            return count;
        }
    }
}
