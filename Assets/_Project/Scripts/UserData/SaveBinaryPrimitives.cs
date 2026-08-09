namespace Gaffer.UserData
{
    /// <summary>
    /// The wire-level constants the binary save container is defined by — the ones a decoder written in any
    /// language would need. They live together because they ARE the format: changing any of them changes
    /// what is on players' devices.
    /// </summary>
    internal static class SaveBinaryPrimitives
    {
        /// <summary>
        /// The four ASCII bytes every binary save starts with: <c>G F S V</c> ("GaFfer SaVe"). It exists so
        /// the loader can tell a binary save from the legacy JSON one by looking at four bytes instead of
        /// guessing from the file name, and so a file that is not a save at all fails immediately with a
        /// clear message rather than deep inside a decode.
        /// </summary>
        internal static readonly byte[] Magic = { 0x47, 0x46, 0x53, 0x56 };

        /// <summary>
        /// Hard ceiling on any single length the file claims, in bytes or elements. Its job is NOT to bound
        /// legitimate data — a name is tens of bytes and the largest planned league world is tens of
        /// thousands of players — but to stop a corrupt varint from being believed: without it, four
        /// flipped bits turn into a two-billion-element list allocation and an <c>OutOfMemoryException</c>
        /// on the boot path, which is the same unhandled crash UNITY.md §7 rules out. Anything above this is
        /// reported as corruption.
        /// </summary>
        internal const int MaxLength = 1 << 22;

        /// <summary>Longest UTF-8 byte length accepted for one string. A club or player name that claims
        /// more than this is corruption, not a long name.</summary>
        internal const int MaxStringBytes = 4096;

        /// <summary>Null, for a field whose absence is meaningful (a strength-only club's squad, a
        /// trait-less player's list, a string the document does not carry).</summary>
        internal const uint NullTag = 0;

        /// <summary>Present, for an optional SECTION rather than an optional value: the record that follows
        /// is there. Deliberately the same number as <see cref="NewInternedTag"/> and for the same reason a
        /// list's "count + 1" encoding works — 0 is reserved for absence, so 1 is the first thing anything
        /// else can mean. Container v2's run block is a nest of these.</summary>
        internal const uint PresentTag = 1;

        /// <summary>An interned string that appears here for the FIRST time: its bytes follow inline and it
        /// is appended to the file's string pool. Every later occurrence is <c>PoolBase + index</c>.</summary>
        internal const uint NewInternedTag = 1;

        /// <summary>The first tag value that means "an index into the string pool". Pool entry <c>i</c> is
        /// written as <c>PoolBase + i</c>, so 0 and 1 stay free for null and "new string follows".</summary>
        internal const uint PoolBase = 2;
    }
}
