namespace Gaffer.Application.Serialization
{
    /// <summary>The current save schema version. Bump it when the save shape changes, and add a migration step.</summary>
    public static class SaveSchema
    {
        // v2: matches are seeded per fixture from a fixed season seed, so the save stores that seed
        // (MatchSeed) instead of an evolving rng state.
        public const int CurrentVersion = 2;
    }
}
