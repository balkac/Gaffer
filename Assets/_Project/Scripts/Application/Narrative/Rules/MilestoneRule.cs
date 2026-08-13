namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// A round-number count reached. One class for both milestones, parameterised by which counter it
    /// watches — the two differ in their numbers, not in their logic, so two near-identical classes would
    /// be duplication rather than clarity.
    /// </summary>
    public sealed class MilestoneRule : IMomentRule
    {
        private readonly int[] _milestones;
        private readonly CareerMomentKind _kind;
        private readonly bool _countsGoals;

        private MilestoneRule(CareerMomentKind kind, bool countsGoals, int[] milestones)
        {
            _kind = kind;
            _countsGoals = countsGoals;
            _milestones = milestones;
        }

        public static MilestoneRule ForAppearances(params int[] milestones)
        {
            return new MilestoneRule(CareerMomentKind.AppearanceMilestone, countsGoals: false, milestones);
        }

        public static MilestoneRule ForGoals(params int[] milestones)
        {
            return new MilestoneRule(CareerMomentKind.GoalMilestone, countsGoals: true, milestones);
        }

        public bool Recognise(in MatchOccasion occasion, out CareerMoment moment)
        {
            int before = _countsGoals ? occasion.GoalsBefore : occasion.AppearancesBefore;
            int after = _countsGoals ? occasion.GoalsAfter : occasion.AppearancesAfter;

            // Crossed, not landed on: a brace can take a striker from 24 goals to 26, and a milestone
            // that only fired on equality would let him pass his twenty-fifth without it being a day.
            for (int i = 0; i < _milestones.Length; i++)
            {
                int milestone = _milestones[i];
                if (before < milestone && after >= milestone)
                {
                    moment = occasion.Moment(_kind, CareerMoment.NoMinute, milestone);
                    return true;
                }
            }

            moment = default;
            return false;
        }
    }
}
