using System.Collections.Generic;
using Gaffer.Application.Narrative;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>
    /// How strong an arc a career is — the one heuristic in the Gate B instrument, kept in a type of its
    /// own so the judgement it encodes can be argued with in one place.
    ///
    /// <para><b>What it is trying to approximate.</b> Gate B asks whether an "Ali Yilmaz arc" emerges by
    /// itself and reads as though it were written. Three things separate a career that reads that way
    /// from a list of fixtures: it goes SOMEWHERE (a debut early, a payoff late), it VARIES (six kinds of
    /// moment beat six goals), and it contains something RARE. So the score rewards span, distinct kinds,
    /// and the scarce moments — and deliberately does not reward sheer volume, because a striker who
    /// scored in thirty games is a statistic, not a story.</para>
    ///
    /// <para>A heuristic and nothing more: it ranks candidates for a human to read. If it ever starts
    /// deciding what the game does, it has escaped its purpose.</para>
    /// </summary>
    public static class StoryArcScoring
    {
        /// <summary>What each kind of moment is worth, by how rare and how load-bearing it is.</summary>
        public static double WeightOf(CareerMomentKind kind)
        {
            switch (kind)
            {
                case CareerMomentKind.Debut:
                    return 1.0;
                case CareerMomentKind.FirstGoal:
                    return 2.0;
                case CareerMomentKind.BigMatchGoal:
                    return 2.5;
                case CareerMomentKind.DerbyGoal:
                    return 2.5;
                case CareerMomentKind.TitleDeciderGoal:
                    return 3.5;
                case CareerMomentKind.RelegationGoal:
                    return 3.0;
                case CareerMomentKind.Brace:
                    return 1.5;
                case CareerMomentKind.Hattrick:
                    return 4.0;
                case CareerMomentKind.AppearanceMilestone:
                    return 2.0;
                case CareerMomentKind.GoalMilestone:
                    return 3.0;
                case CareerMomentKind.Signing:
                    return 1.0;
                case CareerMomentKind.Sale:
                    return 3.0;
                case CareerMomentKind.AcademyArrival:
                    return 1.5;
                case CareerMomentKind.BreakoutSeason:
                    return 3.0;
                case CareerMomentKind.Retirement:
                    return 2.5;
                default:
                    // A kind added tomorrow still counts for something rather than silently scoring zero
                    // and disappearing from every ranking (CONVENTIONS §1: a switch always has a default).
                    return 1.0;
            }
        }

        /// <summary>
        /// The arc score. Weighted moments, multiplied by how many DISTINCT kinds appear and how many
        /// seasons it runs across — both as gentle roots, so variety and longevity lift a career without
        /// letting a long dull one outrank a short vivid one.
        /// </summary>
        public static double Score(IReadOnlyList<CareerMoment> moments, int seasonsSpanned)
        {
            if (moments == null || moments.Count == 0)
            {
                return 0.0;
            }

            double weighted = 0.0;
            var kinds = new HashSet<CareerMomentKind>();
            for (int i = 0; i < moments.Count; i++)
            {
                weighted += WeightOf(moments[i].Kind);
                kinds.Add(moments[i].Kind);
            }

            double variety = System.Math.Sqrt(kinds.Count);
            double longevity = System.Math.Sqrt(seasonsSpanned < 1 ? 1 : seasonsSpanned);
            return weighted * variety * longevity;
        }
    }
}
