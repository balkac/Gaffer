using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
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
