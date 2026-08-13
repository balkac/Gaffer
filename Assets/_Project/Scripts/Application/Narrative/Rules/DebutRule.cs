namespace Gaffer.Application.Narrative
{
    /// <summary>His first game for the club. Every career has exactly one, and only the counter knew.</summary>
    public sealed class DebutRule : IMomentRule
    {
        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            if (occasion.AppearancesBefore > 0)
            {
                moment = default;
                return false;
            }

            moment = occasion.Moment(CareerMomentKind.Debut);
            return true;
        }
    }
}
