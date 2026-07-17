namespace Gaffer.Application.Serialization
{
    /// <summary>The current save schema version. Bump it when the save shape changes, and add a migration step.</summary>
    public static class SaveSchema
    {
        // v2: matches are seeded per fixture from a fixed season seed, so the save stores that seed
        // (MatchSeed) instead of an evolving rng state.
        // v3: full squads and the season number are persisted, so a multi-season run survives development
        // and renewal. A v2 save has no squads (clubs restore as strength only) and no season number.
        // v4: each player persists his trait ids (slugs), so the character layer survives a reload. A v3
        // player has no traits field and restores trait-less.
        public const int CurrentVersion = 4;
    }
}
