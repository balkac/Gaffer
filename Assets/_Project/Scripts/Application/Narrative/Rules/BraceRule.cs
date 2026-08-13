namespace Gaffer.Application.Narrative
{
    /// <summary>Exactly two — a hat-trick is its own moment and does not also count as a brace.</summary>
    public sealed class BraceRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (occasion.GoalsInThisMatch != 2)
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.Brace, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
