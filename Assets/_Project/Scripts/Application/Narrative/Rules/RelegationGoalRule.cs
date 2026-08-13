namespace Gaffer.Application.Narrative
{
    /// <summary>A goal in a relegation six-pointer — the other end of the table, and a different story
    /// from a title decider even though the arithmetic that recognises them is the same shape.</summary>
    public sealed class RelegationGoalRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (!occasion.Scored || !(occasion.WasARelegationSixPointer && !occasion.WasATitleDecider))
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.RelegationGoal, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
