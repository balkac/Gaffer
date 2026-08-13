namespace Gaffer.Application.Narrative
{
    /// <summary>His first game for the club. Every career has exactly one, and only the counter knew.</summary>
    public sealed class DebutRule : IMomentRule
    {
        public bool Recognise(in MomentOccasion occasion, out CareerMoment moment)
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

    /// <summary>His first goal for the club — the one everybody remembers.</summary>
    public sealed class FirstGoalRule : IMomentRule
    {
        public bool Recognise(in MomentOccasion occasion, out CareerMoment moment)
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

    /// <summary>
    /// Three or more in one match. Separate from <see cref="BraceRule"/> rather than one "multiple goals"
    /// rule with a threshold: they are different events to a supporter, and a hat-trick has to be able to
    /// grow its own copy, its own rarity and its own place in a recap without dragging a brace along.
    /// </summary>
    public sealed class HattrickRule : IMomentRule
    {
        public bool Recognise(in MomentOccasion occasion, out CareerMoment moment)
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

    /// <summary>Exactly two — a hat-trick is its own moment and does not also count as a brace.</summary>
    public sealed class BraceRule : IMomentRule
    {
        public bool Recognise(in MomentOccasion occasion, out CareerMoment moment)
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

    /// <summary>
    /// A goal in a fixture that mattered. The one kind of significance a player's own history cannot
    /// supply — it comes from the occasion, which is exactly why the match context is an input.
    /// </summary>
    public sealed class BigMatchGoalRule : IMomentRule
    {
        public bool Recognise(in MomentOccasion occasion, out CareerMoment moment)
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

        public bool Recognise(in MomentOccasion occasion, out CareerMoment moment)
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
