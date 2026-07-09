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

        private static void PlayWholeSeason(LeagueSeason season, ulong seed)
        {
            MatchSimulator simulator = CreateSimulator();
            MatchContext context = NormalContext();

            int guard = 0;
            while (!season.IsComplete && guard < 1000)
            {
                season.AdvanceWeek(simulator, context, seed);
                guard++;
            }
        }

        [Test]
        public void Season_PlayedToCompletion_EveryClubPlaysEveryFixture()
        {
            var season = new LeagueSeason(CreateLeague());

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
            var season = new LeagueSeason(CreateLeague());

            WeekResult week = season.AdvanceWeek(CreateSimulator(), NormalContext(), 1UL);

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
            var withDefault = new LeagueSeason(CreateSquadLeague());
            var withTweak = new LeagueSeason(CreateSquadLeague());
            var tweaked = new ClubId(0);
            withTweak.SetTactics(tweaked, new Tactics(Mentality.VeryAttacking, Tempo.Intense, Pressing.Press, Approach.Possession));

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
            var season = new LeagueSeason(CreateSquadLeague());

            Squad squad = season.SquadOf(new ClubId(0));

            Assert.That(squad, Is.Not.Null);
            Assert.That(squad.Count, Is.EqualTo(SquadGenerator.SquadSize));
        }

        [Test]
        public void UpdateSquad_SwapsRoster_ReflectedBySquadOf()
        {
            var season = new LeagueSeason(CreateSquadLeague());
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
            var season = new LeagueSeason(CreateLeague());

            Assert.That(season.SquadOf(new ClubId(0)), Is.Null);
            Assert.DoesNotThrow(() => season.UpdateSquad(new ClubId(0), CreateStrongSquad(100000)));
            Assert.That(season.SquadOf(new ClubId(0)), Is.Null);
        }

        [Test]
        public void UpdateSquad_ToStrongerRoster_ImprovesThatClubsResults()
        {
            const ulong seed = 555UL;
            var club = new ClubId(0);

            var baseline = new LeagueSeason(CreateSquadLeague());
            PlayWholeSeason(baseline, seed);
            int basePoints = PointsOf(baseline, club);

            var upgraded = new LeagueSeason(CreateSquadLeague());
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
        public void Season_SameSeed_ProducesIdenticalTable()
        {
            var first = new LeagueSeason(CreateLeague());
            var second = new LeagueSeason(CreateLeague());

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
    }
}
