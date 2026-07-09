namespace Gaffer.Application.Serialization
{
    /// <summary>The current save schema version. Bump it when the save shape changes, and add a migration step.</summary>
    public static class SaveSchema
    {
        // v2: matches are seeded per fixture from a fixed season seed, so the save stores that seed
        // (MatchSeed) instead of an evolving rng state.
        // v3: full squads and the season number are persisted, so a multi-season run survives development
        // and renewal. A v2 save has no squads (clubs restore as strength only) and no season number.
        public const int CurrentVersion = 3;
    }
}
