namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// A goal in a fixture that mattered and was none of the three above — a context raised some other
    /// way. The fallback exists so a new kind of occasion is never silently unremarkable while its own
    /// rule is being written.
    /// </summary>
    public sealed class BigMatchGoalRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (!occasion.Scored || !(occasion.WasAnOccasion && !occasion.WasADerby && !occasion.WasATitleDecider && !occasion.WasARelegationSixPointer))
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.BigMatchGoal, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
