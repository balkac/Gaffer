namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// How much a match weighs beyond a routine league fixture. Feeds trait modulation (a derby
    /// monster lifts, a big-game bottler drops) and narrative emphasis (TDD §6). A fixed sim-level
    /// classification, so a plain enum — not data-driven content that would use an Id.
    /// </summary>
    public enum MatchImportance
    {
        Normal,
        Derby,
        Final,
        RelegationSixPointer
    }
}
