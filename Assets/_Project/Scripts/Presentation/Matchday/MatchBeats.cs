using System.Collections.Generic;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;

namespace Gaffer.Presentation.Matchday
{
    /// <summary>One line of the afternoon, as the report reads it out.</summary>
    public readonly struct MatchBeat
    {
        public MatchBeat(int minute, bool isGoal, bool isOurs, string headline, string story)
        {
            Minute = minute;
            IsGoal = isGoal;
            IsOurs = isOurs;
            Headline = headline;
            Story = story;
        }

        public int Minute { get; }

        /// <summary>Ties are broken with this, so a goal is drawn above the story it produced.</summary>
        public bool IsGoal { get; }

        /// <summary>Whether the managed club did it. A goal against is still the afternoon.</summary>
        public bool IsOurs { get; }

        public string Headline { get; }

        /// <summary>The career moment this beat created, or null. Never a beat of its own when a goal
        /// explains it.</summary>
        public string Story { get; }

        public bool HasStory => !string.IsNullOrEmpty(Story);
    }

    /// <summary>
    /// The afternoon, read off a played week.
    ///
    /// <para><b>Separate from the screen that draws it, on purpose.</b> Which beats there are, in which
    /// order, and which moment belongs to which goal is a reading of an outcome and not a drawing
    /// decision. Kept here it is framework-free, which means <c>dotnet test</c> can hold the two rules
    /// that are easy to get quietly wrong — the fold and the order — without opening Unity.</para>
    ///
    /// <para><b>What it can say.</b> The simulation emits one kind of match event, a goal, so the beats
    /// are goals plus the career moments the narrative layer recognised. ART_STYLE's mock also shows
    /// cards, substitutions and missed chances; those are not in the sim and are absent rather than
    /// invented, because a feed that makes up a booking cannot be trusted about a goal
    /// (NON-NEGOTIABLE #7).</para>
    /// </summary>
    public static class MatchBeats
    {
        /// <summary>
        /// Every beat of one match, latest first — the order a broadcast recaps in.
        /// </summary>
        public static List<MatchBeat> Of(
            WeekOutcome week,
            MatchResult match,
            RunSession session,
            LocalizedStrings text)
        {
            var beats = new List<MatchBeat>(match.Events.Count + week.Moments.Count);
            var folded = new List<int>(week.Moments.Count);
            bool weWereHome = session != null && match.Home.Value == session.ManagedClub.Value;

            for (int i = 0; i < match.Events.Count; i++)
            {
                MatchEvent played = match.Events[i];
                if (played.Kind != MatchEventKind.Goal)
                {
                    continue;
                }

                ClubId scoredFor = played.Side == TeamSide.Home ? match.Home : match.Away;
                int story = StoryFor(week, played, folded);

                beats.Add(new MatchBeat(
                    played.Minute,
                    isGoal: true,
                    isOurs: (played.Side == TeamSide.Home) == weWereHome,
                    headline: Headline(played, scoredFor, session, text),
                    story: story >= 0 ? MomentLine.For(week.Moments[story], session, text) : null));
            }

            for (int i = 0; i < week.Moments.Count; i++)
            {
                CareerMoment moment = week.Moments[i];
                if (!moment.HappenedInAMatch || folded.Contains(i))
                {
                    continue;
                }

                // Anything the goals did not explain — a debut, an appearance milestone — stands on its
                // own, because it happened to somebody even though nothing went in. Always ours: the
                // narrative layer only watches the club being managed.
                beats.Add(new MatchBeat(
                    moment.Minute,
                    isGoal: false,
                    isOurs: true,
                    headline: MomentLine.For(moment, session, text),
                    story: null));
            }

            beats.Sort(Compare);
            return beats;
        }

        /// <summary>
        /// The index of the moment this goal created, or -1.
        ///
        /// <para>Matched on minute AND scorer, not on minute alone: two goals can share a minute, and
        /// matching loosely would hand the second scorer the first one's story — a line naming the wrong
        /// player, which is worse than no line at all. A moment is folded once and then spent.</para>
        /// </summary>
        private static int StoryFor(WeekOutcome week, MatchEvent played, List<int> folded)
        {
            if (!played.Scorer.HasValue)
            {
                return -1;
            }

            for (int i = 0; i < week.Moments.Count; i++)
            {
                CareerMoment moment = week.Moments[i];
                if (folded.Contains(i)
                    || moment.Minute != played.Minute
                    || moment.Player.Value != played.Scorer.Value.Value)
                {
                    continue;
                }

                folded.Add(i);
                return i;
            }

            return -1;
        }

        private static string Headline(
            MatchEvent played,
            ClubId scoredFor,
            RunSession session,
            LocalizedStrings text)
        {
            string goal = text.Or(UiTextKeys.MatchGoal);
            if (!played.Scorer.HasValue || session == null)
            {
                return goal;
            }

            string name = session.PlayerName(scoredFor, played.Scorer.Value);
            return string.IsNullOrEmpty(name) ? goal : goal + " · " + name;
        }

        // Latest first, and a goal above the story it produced when both carry the same minute.
        private static int Compare(MatchBeat left, MatchBeat right)
        {
            if (left.Minute != right.Minute)
            {
                return right.Minute.CompareTo(left.Minute);
            }

            if (left.IsGoal == right.IsGoal)
            {
                return 0;
            }

            return left.IsGoal ? -1 : 1;
        }
    }
}
