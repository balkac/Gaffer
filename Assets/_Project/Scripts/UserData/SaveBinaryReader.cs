using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Gaffer.UserData
{
    /// <summary>
    /// The decoder half of the binary save container — the exact mirror of <see cref="SaveBinaryWriter"/>,
    /// little-endian and UTF-8 for the same stated reasons.
    /// <para>
    /// EVERY read is bounds-checked, because the input is a file on a device a player can edit and an app
    /// the OS can kill mid-write. Running off the end of the stream, an overlong varint, a length past
    /// <see cref="SaveBinaryPrimitives.MaxLength"/>, a pool index the file never defined — each one throws
    /// <see cref="MalformedSaveException"/>, which <see cref="BinarySaveSerializer"/> turns into a
    /// <c>Result</c> failure. There is no path here that can produce an <c>IndexOutOfRangeException</c>, an
    /// <c>OutOfMemoryException</c> from a believed length, or a half-decoded document that looks valid
    /// (UNITY.md §7: a corrupt save is an expected failure with a defined fallback, never a crash at boot).
    /// </para>
    /// </summary>
    internal sealed class SaveBinaryReader
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private readonly Stream _stream;
        private readonly List<string> _pool = new List<string>();

        private byte[] _scratch = new byte[256];

        internal SaveBinaryReader(Stream stream)
        {
            _stream = stream;
        }

        /// <summary>Reads the four magic bytes and the container version, and reports whether the magic
        /// matched. A short file fails here rather than being decoded as an empty document.</summary>
        internal bool TryReadHeader(out ushort containerVersion)
        {
            containerVersion = 0;
            for (int i = 0; i < SaveBinaryPrimitives.Magic.Length; i++)
            {
                if (ReadByteOrThrow() != SaveBinaryPrimitives.Magic[i])
                {
                    return false;
                }
            }

            int low = ReadByteOrThrow();
            int high = ReadByteOrThrow();
            containerVersion = (ushort)(low | (high << 8));
            return true;
        }

        internal uint ReadVarUInt32()
        {
            uint value = 0;
            int shift = 0;

            // Five groups of seven bits is the most a 32-bit value can occupy; a sixth continuation byte
            // means the bytes are not a varint at all.
            for (int i = 0; i < 5; i++)
            {
                int b = ReadByteOrThrow();
                value |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    return value;
                }

                shift += 7;
            }

            throw new MalformedSaveException("Save contains a number longer than a 32-bit varint can be.");
        }

        internal int ReadInt32()
        {
            return unchecked((int)ReadVarUInt32());
        }

        /// <summary>A varint used as a length or a count, checked against the format's ceiling BEFORE the
        /// caller allocates anything sized by it.</summary>
        internal int ReadLength(string what)
        {
            uint value = ReadVarUInt32();
            if (value > SaveBinaryPrimitives.MaxLength)
            {
                throw new MalformedSaveException("Save claims " + value + " " + what + ", which is not a real save.");
            }

            return (int)value;
        }

        internal ulong ReadUInt64()
        {
            ulong value = 0;
            for (int i = 0; i < 8; i++)
            {
                value |= (ulong)ReadByteOrThrow() << (i * 8);
            }

            return value;
        }

        internal double ReadDouble()
        {
            return BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64()));
        }

        internal byte ReadByte()
        {
            return (byte)ReadByteOrThrow();
        }

        internal void ReadBytes(byte[] destination, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                int got = _stream.Read(destination, offset + read, count - read);
                if (got <= 0)
                {
                    throw new MalformedSaveException("Save ends part-way through a record; the file is truncated.");
                }

                read += got;
            }
        }

        internal string ReadString()
        {
            uint tag = ReadVarUInt32();
            if (tag == SaveBinaryPrimitives.NullTag)
            {
                return null;
            }

            return ReadText((int)(tag - 1));
        }

        internal string ReadInternedString()
        {
            uint tag = ReadVarUInt32();
            if (tag == SaveBinaryPrimitives.NullTag)
            {
                return null;
            }

            if (tag == SaveBinaryPrimitives.NewInternedTag)
            {
                string text = ReadText(ReadLength("bytes of text"));
                _pool.Add(text);
                return text;
            }

            int index = (int)(tag - SaveBinaryPrimitives.PoolBase);
            if (index >= _pool.Count)
            {
                // Only reachable from a damaged or hand-edited file: the writer never emits an index before
                // the string it points at. Naming the index makes the corruption diagnosable from the log.
                throw new MalformedSaveException(
                    "Save refers to text #" + index + " before defining it; the file is damaged.");
            }

            return _pool[index];
        }

        private string ReadText(int byteCount)
        {
            if (byteCount > SaveBinaryPrimitives.MaxStringBytes)
            {
                throw new MalformedSaveException(
                    "Save claims a " + byteCount + "-byte string, past the format's limit of "
                    + SaveBinaryPrimitives.MaxStringBytes + ".");
            }

            if (byteCount == 0)
            {
                return string.Empty;
            }

            if (byteCount > _scratch.Length)
            {
                _scratch = new byte[byteCount];
            }

            ReadBytes(_scratch, 0, byteCount);
            return Utf8.GetString(_scratch, 0, byteCount);
        }

        private int ReadByteOrThrow()
        {
            int b = _stream.ReadByte();
            if (b < 0)
            {
                throw new MalformedSaveException("Save ends part-way through a record; the file is truncated.");
            }

            return b;
        }
    }
}
