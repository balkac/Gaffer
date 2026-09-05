using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class LeagueSeasonTests
    {
        private const int ClubCount = 20;

        private static League CreateLeague()
        {
            var clubs = new List<Club>(ClubCount);
            for (int i = 0; i < ClubCount; i++)
            {
                double quality = 70.0 - i * (24.0 / (ClubCount - 1));
                clubs.Add(new Club(new ClubId(i), "Club " + (i + 1), new TeamStrength(quality, quality, quality)));
            }

            return new League("Test League", clubs);
        }

        private static MatchSimulator CreateSimulator()
        {
            return new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
        }

        private static MatchContext NormalContext()
        {
            return new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
        }

        // The season owns its simulator (ARCHITECTURE §6); every season in this file is built here so the
        // wiring is stated once.
        private static LeagueSeason PlayableSeason(League league)
        {
            return new LeagueSeason(league, null, null, null, CreateSimulator());
        }

        private static void PlayWholeSeason(LeagueSeason season, ulong seed)
        {
            MatchContext context = NormalContext();

            int guard = 0;
            while (!season.IsComplete && guard < 1000)
            {
                season.AdvanceWeek(context, seed);
                guard++;
            }
        }

        [Test]
        public void Season_PlayedToCompletion_EveryClubPlaysEveryFixture()
        {
            var season = PlayableSeason(CreateLeague());

            PlayWholeSeason(season, 2024UL);

            Assert.That(season.IsComplete, Is.True);
            Assert.That(season.RoundCount, Is.EqualTo(2 * (ClubCount - 1)));

            IReadOnlyList<LeagueTableRow> table = season.Table.Ordered();
            Assert.That(table.Count, Is.EqualTo(ClubCount));
            foreach (LeagueTableRow row in table)
            {
                Assert.That(row.Played, Is.EqualTo(2 * (ClubCount - 1)));
                Assert.That(row.Won + row.Drawn + row.Lost, Is.EqualTo(row.Played));
            }
        }

        [Test]
        public void AdvanceWeek_FirstRound_PlaysEveryClubOnce()
        {
            var season = PlayableSeason(CreateLeague());

            WeekResult week = season.AdvanceWeek(NormalContext(), 1UL);

            Assert.That(week.Round, Is.EqualTo(0));
            Assert.That(week.Matches.Count, Is.EqualTo(ClubCount / 2));
            Assert.That(season.CurrentRound, Is.EqualTo(1));
        }

        private static League CreateSquadLeague()
        {
            var squadGen = new SquadGenerator(new PlayerGenerator());
            var builder = new EffectiveStrengthBuilder();
            var genRng = new SplitMix64RandomNumberGenerator(4242UL);
            var clubs = new List<Club>(ClubCount);
            for (int i = 0; i < ClubCount; i++)
            {
                Squad squad = squadGen.Generate(i * SquadGenerator.SquadSize, new GenerationContext(), genRng);
                clubs.Add(new Club(new ClubId(i), "Club " + i, squad, builder.Build(squad)));
            }

            return new League("Squad League", clubs);
        }

        [Test]
        public void AdvanceWeek_ChangingOneClubsTactics_LeavesMatchesWithoutItIdentical()
        {
            const ulong seed = 909UL;
            var withDefault = PlayableSeason(CreateSquadLeague());
            var withTweak = PlayableSeason(CreateSquadLeague());
            var tweaked = new ClubId(0);
            withTweak.SetTactics(tweaked, new Tactics(Mentality.VeryAttacking, Tempo.Intense, Pressing.Press, Approach.Possession));

            // The run-in is switched off in BOTH seasons, and it is the isolation this test claims that
            // requires it. Late in a season a fixture's meaning is read off the table (MatchContextBuilder:
            // two contenders in May are playing a decider, two clubs in the drop a six-pointer), so club 0
            // taking three points it did not take before can move a third club across the contender line and
            // change a match club 0 is not in. That coupling is deliberate and correct — a league where the
            // table means nothing is not a league — but it is a different property from the one asserted
            // here, which is that per-fixture seeding keeps unrelated fixtures byte-identical. Left on, this
            // test passes or fails on whether any fixture happens to be sitting on a boundary.
            var neutral = new MatchContextBuilder(RivalryTable.None, new MatchContextSettings(runInRounds: 0));
            withDefault.SetMatchContextBuilder(neutral);
            withTweak.SetMatchContextBuilder(neutral);

            PlayWholeSeason(withDefault, seed);
            PlayWholeSeason(withTweak, seed);

            // Per-fixture seeding means only club 0's matches can differ; every other fixture is byte-identical.
            IReadOnlyList<MatchResult> a = withDefault.PlayedResults;
            IReadOnlyList<MatchResult> b = withTweak.PlayedResults;
            Assert.That(a.Count, Is.EqualTo(b.Count));
            int comparedWithout = 0;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].Home.Value == tweaked.Value || a[i].Away.Value == tweaked.Value)
                {
                    continue;
                }

                Assert.That(b[i].Home.Value, Is.EqualTo(a[i].Home.Value));
                Assert.That(b[i].Away.Value, Is.EqualTo(a[i].Away.Value));
                Assert.That(b[i].HomeGoals, Is.EqualTo(a[i].HomeGoals));
                Assert.That(b[i].AwayGoals, Is.EqualTo(a[i].AwayGoals));
                Assert.That(b[i].HomeShots, Is.EqualTo(a[i].HomeShots));
                comparedWithout++;
            }

            Assert.That(comparedWithout, Is.GreaterThan(0), "Expected some matches not involving the tweaked club.");
        }

        private static Squad CreateStrongSquad(int idBase)
        {
            var squadGen = new SquadGenerator(new PlayerGenerator());
            var genRng = new SplitMix64RandomNumberGenerator(99UL);
            var context = new GenerationContext { MinAbility = 90, MaxAbility = 99, MinAge = 24, MaxAge = 28 };
            return squadGen.Generate(idBase, context, genRng);
        }

        [Test]
        public void SquadOf_ForSquadClub_ReturnsRoster()
        {
            var season = PlayableSeason(CreateSquadLeague());

            Squad squad = season.SquadOf(new ClubId(0));

            Assert.That(squad, Is.Not.Null);
            Assert.That(squad.Count, Is.EqualTo(SquadGenerator.SquadSize));
        }

        [Test]
        public void UpdateSquad_SwapsRoster_ReflectedBySquadOf()
        {
            var season = PlayableSeason(CreateSquadLeague());
            var club = new ClubId(0);
            Squad original = season.SquadOf(club);
            PlayerId sold = original.Players[0].Id;

            season.UpdateSquad(club, original.Remove(sold));

            Squad after = season.SquadOf(club);
            Assert.That(after.Count, Is.EqualTo(original.Count - 1));
            Assert.That(after.Contains(sold), Is.False);
        }

        [Test]
        public void UpdateSquad_SquadlessClub_IsNoOp()
        {
            var season = PlayableSeason(CreateLeague());

            Assert.That(season.SquadOf(new ClubId(0)), Is.Null);
            Assert.DoesNotThrow(() => season.UpdateSquad(new ClubId(0), CreateStrongSquad(100000)));
            Assert.That(season.SquadOf(new ClubId(0)), Is.Null);
        }

        [Test]
        public void UpdateSquad_ToStrongerRoster_ImprovesThatClubsResults()
        {
            const ulong seed = 555UL;
            var club = new ClubId(0);

            var baseline = PlayableSeason(CreateSquadLeague());
            PlayWholeSeason(baseline, seed);
            int basePoints = PointsOf(baseline, club);

            var upgraded = PlayableSeason(CreateSquadLeague());
            upgraded.UpdateSquad(club, CreateStrongSquad(100000));
            PlayWholeSeason(upgraded, seed);
            int upgradedPoints = PointsOf(upgraded, club);

            // A live roster swap must re-derive the club's strength from the new eleven, so a maxed-out
            // squad clearly outperforms the mid-table one it replaced — the signing takes effect on the pitch.
            Assert.That(upgradedPoints, Is.GreaterThan(basePoints));
        }

        private static int PointsOf(LeagueSeason season, ClubId club)
        {
            foreach (LeagueTableRow row in season.Table.Ordered())
            {
                if (row.Club.Value == club.Value)
                {
                    return row.Points;
                }
            }

            return -1;
        }

        [Test]
        public void AdvanceWeek_CachedAutoPickedEleven_MatchesReSelectingItEveryRound()
        {
            // The auto-picked eleven is memoised per club, which is only sound because SelectBest is a
            // pure function of (squad, formation). The control season re-declares each club's formation
            // before every round, which drops the memo and forces a fresh pick each week — the exact
            // work the cache removes. Every result must be byte-identical, or the cache is not
            // behaviour-preserving (NON-NEGOTIABLE #2).
            const ulong seed = 31337UL;
            var cached = PlayableSeason(CreateSquadLeague());
            var recomputed = PlayableSeason(CreateSquadLeague());

            PlayWholeSeason(cached, seed);

            MatchContext context = NormalContext();
            int guard = 0;
            while (!recomputed.IsComplete && guard < 1000)
            {
                for (int i = 0; i < ClubCount; i++)
                {
                    // Same formation the auto-pick defaults to; the point is the invalidation, not a change.
                    recomputed.SetFormation(new ClubId(i), Formation.F442);
                }

                recomputed.AdvanceWeek(context, seed);
                guard++;
            }

            AssertSameResults(cached.PlayedResults, recomputed.PlayedResults);
        }

        private static void AssertSameResults(IReadOnlyList<MatchResult> expected, IReadOnlyList<MatchResult> actual)
        {
            Assert.That(actual.Count, Is.EqualTo(expected.Count));
            for (int i = 0; i < expected.Count; i++)
            {
                Assert.That(actual[i].Home.Value, Is.EqualTo(expected[i].Home.Value), $"Match {i} home club.");
                Assert.That(actual[i].Away.Value, Is.EqualTo(expected[i].Away.Value), $"Match {i} away club.");
                Assert.That(actual[i].HomeGoals, Is.EqualTo(expected[i].HomeGoals), $"Match {i} home goals.");
                Assert.That(actual[i].AwayGoals, Is.EqualTo(expected[i].AwayGoals), $"Match {i} away goals.");
                Assert.That(actual[i].HomeShots, Is.EqualTo(expected[i].HomeShots), $"Match {i} home shots.");
                Assert.That(actual[i].AwayShots, Is.EqualTo(expected[i].AwayShots), $"Match {i} away shots.");
                Assert.That(actual[i].Events.Count, Is.EqualTo(expected[i].Events.Count), $"Match {i} event count.");
                for (int e = 0; e < expected[i].Events.Count; e++)
                {
                    Assert.That(actual[i].Events[e].Minute, Is.EqualTo(expected[i].Events[e].Minute), $"Match {i} event {e} minute.");
                    Assert.That(actual[i].Events[e].Side, Is.EqualTo(expected[i].Events[e].Side), $"Match {i} event {e} side.");
                    Assert.That(actual[i].Events[e].Scorer?.Value, Is.EqualTo(expected[i].Events[e].Scorer?.Value), $"Match {i} event {e} scorer.");
                }
            }
        }

        [Test]
        public void UpdateSquad_MidSeason_DropsTheCachedElevenAndFieldsTheNewRoster()
        {
            // The regression the eleven-cache can cause: a club whose roster is replaced after the cache
            // was already filled keeps fielding its old eleven for the rest of the season. So the swap
            // happens AFTER rounds have been played, and the club must both diverge from the control at
            // that point and clearly outperform it from there on.
            const ulong seed = 8080UL;
            const int swapAfterRounds = 2;
            var club = new ClubId(0);
            MatchContext context = NormalContext();

            var control = PlayableSeason(CreateSquadLeague());
            var swapped = PlayableSeason(CreateSquadLeague());
            for (int round = 0; round < swapAfterRounds; round++)
            {
                control.AdvanceWeek(context, seed);
                swapped.AdvanceWeek(context, seed);
            }

            // Up to the swap the two seasons are the same season.
            AssertSameResults(control.PlayedResults, swapped.PlayedResults);
            int playedBeforeSwap = swapped.PlayedResults.Count;

            swapped.UpdateSquad(club, CreateStrongSquad(100000));
            PlayWholeSeason(control, seed);
            PlayWholeSeason(swapped, seed);

            Assert.That(PointsAfter(swapped, club, playedBeforeSwap), Is.GreaterThan(PointsAfter(control, club, playedBeforeSwap)),
                "A roster swapped in mid-season did not take effect — the cached eleven outlived its squad.");
        }

        // The club's points from the results played after the given index, counted from the result history
        // so the pre-swap rounds (identical in both seasons) do not dilute the comparison.
        private static int PointsAfter(LeagueSeason season, ClubId club, int fromIndex)
        {
            int points = 0;
            IReadOnlyList<MatchResult> results = season.PlayedResults;
            for (int i = fromIndex; i < results.Count; i++)
            {
                MatchResult result = results[i];
                bool isHome = result.Home.Value == club.Value;
                if (!isHome && result.Away.Value != club.Value)
                {
                    continue;
                }

                int scored = isHome ? result.HomeGoals : result.AwayGoals;
                int conceded = isHome ? result.AwayGoals : result.HomeGoals;
                points += scored > conceded ? 3 : (scored == conceded ? 1 : 0);
            }

            return points;
        }

        [Test]
        public void Season_SameSeed_ProducesIdenticalTable()
        {
            var first = PlayableSeason(CreateLeague());
            var second = PlayableSeason(CreateLeague());

            PlayWholeSeason(first, 777UL);
            PlayWholeSeason(second, 777UL);

            IReadOnlyList<LeagueTableRow> firstTable = first.Table.Ordered();
            IReadOnlyList<LeagueTableRow> secondTable = second.Table.Ordered();

            for (int i = 0; i < firstTable.Count; i++)
            {
                Assert.That(firstTable[i].Club.Value, Is.EqualTo(secondTable[i].Club.Value));
                Assert.That(firstTable[i].Points, Is.EqualTo(secondTable[i].Points));
            }
        }

        // A 22-man squad split in two: eleven world-class players and eleven who should never play. The
        // ordinary generated squad rates everyone between 46 and 64, where the worst legal eleven is only
        // ~3 points of strength off the best and a season of that is inside the noise — which is exactly
        // how "SetStarters is ignored" could hide. This roster makes the difference impossible to miss.
        private static Squad BimodalSquad()
        {
            var squadGen = new SquadGenerator(new PlayerGenerator());
            var rng = new SplitMix64RandomNumberGenerator(1234UL);
            Squad elite = squadGen.Generate(200000, new GenerationContext { MinAbility = 90, MaxAbility = 99, MinAge = 24, MaxAge = 28 }, rng);
            Squad reserves = squadGen.Generate(300000, new GenerationContext { MinAbility = 25, MaxAbility = 35, MinAge = 24, MaxAge = 28 }, rng);

            var players = new List<Player>(22);
            for (int i = 0; i < 11; i++)
            {
                players.Add(elite.Players[i]);
                players.Add(reserves.Players[i]);
            }

            return new Squad(players);
        }

        // The eleven weakest players in a squad, worst first — the opposite of what the auto-pick does.
        private static IReadOnlyList<Player> WorstEleven(Squad squad)
        {
            var ordered = new List<Player>(squad.Players);
            ordered.Sort((left, right) => PlayerRatings.ForRole(left).CompareTo(PlayerRatings.ForRole(right)));
            return ordered.GetRange(0, 11);
        }

        [Test]
        public void SetStarters_PinsTheElevenTheClubFields_AndCostsItPoints()
        {
            // StrengthOf branches on the starters map, but nothing in the suite ever put anything in it.
            // Fielding the reserves instead of the first team must cost real points over the same league,
            // seed and fixtures.
            const ulong seed = 4711UL;
            var club = new ClubId(0);

            LeagueSeason autoPicked = PlayableSeason(CreateSquadLeague());
            LeagueSeason handPicked = PlayableSeason(CreateSquadLeague());
            autoPicked.UpdateSquad(club, BimodalSquad());
            handPicked.UpdateSquad(club, BimodalSquad());
            handPicked.SetStarters(club, WorstEleven(handPicked.SquadOf(club)));

            PlayWholeSeason(autoPicked, seed);
            PlayWholeSeason(handPicked, seed);

            Assert.That(PointsOf(handPicked, club), Is.LessThan(PointsOf(autoPicked, club)),
                "A deliberately terrible eleven must reach the pitch.");
        }

        [Test]
        public void SetStarters_ThenUpdateSquad_DropsThePinnedElevenAndReAutoPicks()
        {
            // The pinned lineup names players who may no longer be at the club after a sale, so a roster
            // change has to release it. Same fixture as above, but the squad is re-set right after the
            // eleven is pinned: the club must be back to its auto-picked strength, byte for byte.
            const ulong seed = 4711UL;
            var club = new ClubId(0);

            LeagueSeason control = PlayableSeason(CreateSquadLeague());
            LeagueSeason released = PlayableSeason(CreateSquadLeague());
            control.UpdateSquad(club, BimodalSquad());
            released.UpdateSquad(club, BimodalSquad());
            released.SetStarters(club, WorstEleven(released.SquadOf(club)));
            released.UpdateSquad(club, released.SquadOf(club));

            PlayWholeSeason(control, seed);
            PlayWholeSeason(released, seed);

            AssertSameResults(control.PlayedResults, released.PlayedResults);
        }

        [Test]
        public void SetFormation_ChangesWhichElevenIsAutoPicked_AndTheSeasonWithIt()
        {
            // The formation decides the shape SelectBest fills, so a defensive shape and an attacking one
            // cannot field the same eleven from the same 20-man squad — and therefore cannot play the
            // same season.
            const ulong seed = 4711UL;
            var club = new ClubId(0);

            LeagueSeason flat = PlayableSeason(CreateSquadLeague());
            LeagueSeason narrow = PlayableSeason(CreateSquadLeague());
            flat.SetFormation(club, Formation.F442);
            narrow.SetFormation(club, Formation.F532);

            PlayWholeSeason(flat, seed);
            PlayWholeSeason(narrow, seed);

            IReadOnlyList<MatchResult> a = flat.PlayedResults;
            IReadOnlyList<MatchResult> b = narrow.PlayedResults;
            bool anyDifference = false;
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i].HomeGoals != b[i].HomeGoals || a[i].AwayGoals != b[i].AwayGoals
                    || a[i].HomeShots != b[i].HomeShots || a[i].AwayShots != b[i].AwayShots)
                {
                    anyDifference = true;
                    break;
                }
            }

            Assert.That(anyDifference, Is.True, "Changing a club's formation changed nothing on the pitch.");
        }

        [Test]
        public void AdvanceWeek_OnASeasonWiredWithoutASimulator_Throws()
        {
            // A season built for its table and history alone (SeasonSaveMapper does this) cannot play.
            // Fail fast rather than return an empty week that reads like a finished season.
            var carrier = new LeagueSeason(CreateLeague(), null, null, null, null);

            Assert.That(
                () => carrier.AdvanceWeek(NormalContext(), 1UL),
                Throws.InstanceOf<System.InvalidOperationException>());
        }
    }
}
