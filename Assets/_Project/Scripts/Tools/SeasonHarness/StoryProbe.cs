using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>
    /// The Gate B instrument: plays a run for real, then hands back the strongest careers that came out
    /// of it, so <em>"does a story emerge, and does it read as though it were written?"</em> can be
    /// answered by running a command rather than by playing five seasons and squinting.
    ///
    /// <para><b>Built before the copy, on purpose.</b> The drama text had the same problem and it was the
    /// owner who found it, by playing. The point of having this first is to be able to read the arcs the
    /// simulation actually produces while the words are still being chosen — and to see the failure Gate B
    /// is really guarding against, which is not bad prose but a world where every career looks the same.</para>
    ///
    /// <para>Pure and deterministic like the rest of the harness: same seed, same careers. It plays
    /// through <see cref="RunSession"/> — the real one, not a copy of the loop — so what it measures is
    /// the game (ARCHITECTURE §8a).</para>
    /// </summary>
    public sealed class StoryProbe
    {
        /// <summary>
        /// Plays <paramref name="seasons"/> seasons and returns every career it followed, strongest arc
        /// first. Drama is silenced: it blocks the week on a decision nobody is here to answer.
        /// </summary>
        public StoryReport Measure(int teamCount, int seasons, ulong seed, int marketSize, int guaranteedGems)
        {
            var setup = new RunSetup(
                teamCount: teamCount,
                seed: seed,
                managedClubIndex: 0,
                promotionPosition: 2,
                survivalPosition: teamCount - 3,
                startingCash: 20_000_000L,
                weeklyWageBudget: 900_000L,
                marketSize: marketSize,
                guaranteedGems: guaranteedGems);

            Result<RunSession> started = RunSessionFactory.Start(setup, new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));
            if (started.IsFailure)
            {
                return new StoryReport(started.Error, 0, 0, new List<StoryArc>());
            }

            RunSession session = started.Value;
            for (int season = 0; season < seasons; season++)
            {
                session.AdvanceToEndOfSeason();
                if (season < seasons - 1)
                {
                    session.StartNextSeason();
                }
            }

            return BuildReport(session, seasons);
        }

        private static StoryReport BuildReport(RunSession session, int seasons)
        {
            var arcs = new List<StoryArc>();
            int momentCount = 0;

            foreach (PlayerJourney journey in session.Journeys.Journeys)
            {
                IReadOnlyList<CareerMoment> moments = journey.Moments;
                momentCount += moments.Count;
                if (moments.Count == 0)
                {
                    continue;
                }

                int first = moments[0].Season;
                int last = moments[0].Season;
                for (int i = 1; i < moments.Count; i++)
                {
                    if (moments[i].Season < first)
                    {
                        first = moments[i].Season;
                    }

                    if (moments[i].Season > last)
                    {
                        last = moments[i].Season;
                    }
                }

                int spanned = (last - first) + 1;
                arcs.Add(new StoryArc(
                    journey.Player,
                    // The journey's own snapshot, not a squad lookup: the strongest arcs belong to players
                    // who have long since retired or been sold, and asking the club who is on its books
                    // answered "(left the club)" for every one of them.
                    journey.Name,
                    moments,
                    StoryArcScoring.Score(moments, spanned),
                    first,
                    last));
            }

            arcs.Sort(ByScoreDescending);
            return new StoryReport(session.ManagedClubName, seasons, momentCount, arcs, session.Journeys);
        }

        // A cached comparison delegate rather than a comparer object or a lambda per call — the
        // allocation-free sorting overload (PERFORMANCE §8).
        private static readonly System.Comparison<StoryArc> ByScoreDescending = (left, right) => right.Score.CompareTo(left.Score);
    }
}
