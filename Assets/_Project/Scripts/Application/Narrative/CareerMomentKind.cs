namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// The kinds of moment a career can produce — the vocabulary the narrative layer recognises.
    ///
    /// <para><b>Recognised, not generated.</b> Nothing here invents anything. The match was already
    /// played and the goal was already scored; this names the ones that MEAN something. A goal is a
    /// fact — minute, side, scorer. "His first derby goal, at nineteen" is a moment: the same goal, plus
    /// who he was, what the match was, and that it had never happened before. Most goals are not moments,
    /// and that is the point — a log that records every one is a list, and nothing in a list feels rare
    /// (GDD §4.8).</para>
    ///
    /// <para><b>Persisted by name, never by ordinal</b> (NON-NEGOTIABLE #9), so this list may be
    /// reordered or added to without rewriting anyone's history.</para>
    /// </summary>
    public enum CareerMomentKind
    {
        /// <summary>His first appearance for the club. Every career has exactly one.</summary>
        Debut = 0,

        /// <summary>His first goal for the club — the one everybody remembers.</summary>
        FirstGoal = 1,

        /// <summary>
        /// A goal in a fixture that mattered in some way not covered below. <b>Nothing recognises this
        /// today</b>, and the rule that tried to was provably unreachable: the three kinds below are the
        /// only ways a league fixture is raised, so "an occasion, but none of those" is a contradiction
        /// (see <see cref="MatchOccasion.WasAnOccasion"/>). The kind stays so saves carrying one still
        /// parse, and so a competition that raises a match some other way — a cup final, one day — has a
        /// name waiting for it.
        /// </summary>
        BigMatchGoal = 2,

        /// <summary>A goal in the derby. The fixture a club carries all run, so this one repeats — and
        /// repeats meaningfully, which is why it is its own kind rather than "a big match".</summary>
        DerbyGoal = 12,

        /// <summary>A goal in a match that decided something at the top of the table.</summary>
        TitleDeciderGoal = 13,

        /// <summary>A goal in a relegation six-pointer — the other end, and a different story.</summary>
        RelegationGoal = 14,

        /// <summary>
        /// Two in one match. <b>Nothing recognises this today</b>, deliberately: reading Gate B's output
        /// showed a brace was 20.2% of every moment in the game, because a good striker scores twice
        /// several times a season. The kind is kept rather than deleted so saves that already carry one
        /// still parse, and so a future rule can re-introduce it under a condition that makes it rare —
        /// his first, say. A brace in a fixture worth naming is already told by the occasion rules.
        /// </summary>
        Brace = 3,

        /// <summary>Three in one match. Rare enough to carry a season on its own.</summary>
        Hattrick = 4,

        /// <summary>An appearance milestone — his 25th, 50th, 100th game for the club.</summary>
        AppearanceMilestone = 5,

        /// <summary>A goals milestone at the club.</summary>
        GoalMilestone = 6,

        /// <summary>He arrived: signed from the market.</summary>
        Signing = 7,

        /// <summary>He left, and for how much. The moment the discover-grow-sell flip pays out.</summary>
        Sale = 8,

        /// <summary>He came through the academy rather than being bought.</summary>
        AcademyArrival = 9,

        /// <summary>A season in which his ability jumped — the year he kicked on.</summary>
        BreakoutSeason = 10,

        /// <summary>He hung up his boots.</summary>
        Retirement = 11,
    }
}
