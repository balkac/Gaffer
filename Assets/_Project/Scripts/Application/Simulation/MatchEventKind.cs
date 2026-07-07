namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The kind of key moment a match event records. Goals only for the skeleton; cards, injuries and
    /// substitutions arrive with the event layer (TDD §4.1 step 5).
    /// </summary>
    public enum MatchEventKind
    {
        Goal
    }
}
