namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// The GDD §4.7 event families. A fixed classification the engine and narrative group by — not
    /// open content, so a plain enum (the events themselves are data and use ids).
    /// </summary>
    /// <remarks>
    /// The numeric values are a PERSISTENCE CONTRACT. This enum is authored into drama `.asset` files
    /// (<c>DramaEventSO.category</c>) and Unity serializes an enum field as its ORDINAL — reordering these
    /// members silently re-categorises every shipped asset (UNITY.md §7). Never reorder, renumber, or reuse
    /// a value; only append. <c>PersistedEnumValueTests</c> pins every member so a reorder fails loudly.
    /// </remarks>
    public enum DramaCategory
    {
        Personal = 0,
        Institutional = 1,
        FansMedia = 2,
        Relationship = 3,
        Rivalry = 4,
    }
}
