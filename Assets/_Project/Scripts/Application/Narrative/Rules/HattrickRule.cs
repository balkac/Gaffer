namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// Three or more in one match. Separate from <see cref="BraceRule"/> rather than one "multiple goals"
    /// rule with a threshold: they are different events to a supporter, and a hat-trick has to be able to
    /// grow its own copy, its own rarity and its own place in a recap without dragging a brace along.
    /// </summary>
    public sealed class HattrickRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (occasion.GoalsInThisMatch < 3)
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.Hattrick, occasion.FirstGoalMinute, occasion.GoalsInThisMatch);
            return true;
        }
    }
}
