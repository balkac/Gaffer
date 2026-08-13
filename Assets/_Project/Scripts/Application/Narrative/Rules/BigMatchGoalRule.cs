namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// A goal in a fixture that mattered. The one kind of significance a player's own history cannot
    /// supply — it comes from the occasion, which is exactly why the match context is an input.
    /// </summary>
    public sealed class BigMatchGoalRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (!occasion.Scored || !occasion.IsABigMatch)
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.BigMatchGoal, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
