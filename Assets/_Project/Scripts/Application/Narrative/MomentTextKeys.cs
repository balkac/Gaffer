namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// The localization key each kind of moment is told with — and nothing else. The core names the
    /// line; choosing the words is Presentation's job through the string table
    /// (NON-NEGOTIABLE #8), which is what lets the same career read natively in English and Turkish
    /// rather than in English with Turkish substituted into it.
    ///
    /// <para><b>One key per kind, resolved by a switch with a default.</b> The default is not defensive
    /// noise: the vocabulary of moments is expected to grow, and a kind added tomorrow that fell through
    /// here would produce an empty line in the middle of a career with nothing raised anywhere
    /// (CONVENTIONS §1). It returns a key that has no words, so the copy guard fails loudly in
    /// <c>dotnet test</c> instead of the game showing a blank.</para>
    /// </summary>
    public static class MomentTextKeys
    {
        /// <summary>The prefix every moment key carries, so the table's narrative rows are one block.</summary>
        public const string Prefix = "moment.";

        public static string For(CareerMomentKind kind)
        {
            switch (kind)
            {
                case CareerMomentKind.Debut:
                    return Prefix + "debut";
                case CareerMomentKind.FirstGoal:
                    return Prefix + "first_goal";
                case CareerMomentKind.BigMatchGoal:
                    return Prefix + "big_match_goal";
                case CareerMomentKind.DerbyGoal:
                    return Prefix + "derby_goal";
                case CareerMomentKind.TitleDeciderGoal:
                    return Prefix + "title_decider_goal";
                case CareerMomentKind.RelegationGoal:
                    return Prefix + "relegation_goal";
                case CareerMomentKind.Brace:
                    return Prefix + "brace";
                case CareerMomentKind.Hattrick:
                    return Prefix + "hattrick";
                case CareerMomentKind.AppearanceMilestone:
                    return Prefix + "appearance_milestone";
                case CareerMomentKind.GoalMilestone:
                    return Prefix + "goal_milestone";
                case CareerMomentKind.Signing:
                    return Prefix + "signing";
                case CareerMomentKind.Sale:
                    return Prefix + "sale";
                case CareerMomentKind.AcademyArrival:
                    return Prefix + "academy_arrival";
                case CareerMomentKind.BreakoutSeason:
                    return Prefix + "breakout_season";
                case CareerMomentKind.Retirement:
                    return Prefix + "retirement";
                default:
                    return Prefix + "unknown";
            }
        }
    }
}
