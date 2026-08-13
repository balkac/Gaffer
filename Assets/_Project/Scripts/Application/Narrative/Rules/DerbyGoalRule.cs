namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// A goal in the derby — the fixture a club carries all run. Its own kind rather than "a big match",
    /// because it RECURS: the rivalry is fixed and the schedule is deterministic, so it lands on the same
    /// round every season, and a career that prints one generic sentence four times is a fixture list
    /// wearing prose. Given its own line, four derby goals read as a habit; that is a story.
    /// </summary>
    public sealed class DerbyGoalRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (!occasion.Scored || !(occasion.WasADerby && !occasion.WasATitleDecider && !occasion.WasARelegationSixPointer))
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.DerbyGoal, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
