namespace Gaffer.Application.Narrative
{
    /// <summary>A goal in a match that decided something at the top of the table.</summary>
    public sealed class TitleDeciderGoalRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (!occasion.Scored || !(occasion.WasATitleDecider))
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.TitleDeciderGoal, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
