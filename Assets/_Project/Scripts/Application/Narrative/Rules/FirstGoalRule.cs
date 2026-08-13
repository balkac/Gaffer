namespace Gaffer.Application.Narrative
{
    /// <summary>His first goal for the club — the one everybody remembers.</summary>
    public sealed class FirstGoalRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (!occasion.Scored || occasion.GoalsBefore > 0)
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.FirstGoal, occasion.FirstGoalMinute);
            return true;
        }
    }
}
