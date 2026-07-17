namespace Gaffer.Domain.Drama
{
    /// <summary>
    /// The GDD §4.7 event families. A fixed classification the engine and narrative group by — not
    /// open content, so a plain enum (the events themselves are data and use ids).
    /// </summary>
    public enum DramaCategory
    {
        Personal,
        Institutional,
        FansMedia,
        Relationship,
        Rivalry,
    }
}
