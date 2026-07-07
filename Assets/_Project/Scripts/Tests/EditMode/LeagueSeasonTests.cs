using System.Collections.Generic;
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
            var rng = new SplitMix64RandomNumberGenerator(seed);

            int guard = 0;
            while (!season.IsComplete && guard < 1000)
            {
                season.AdvanceWeek(simulator, context, rng);
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

            WeekResult week = season.AdvanceWeek(CreateSimulator(), NormalContext(), new SplitMix64RandomNumberGenerator(1));

            Assert.That(week.Round, Is.EqualTo(0));
            Assert.That(week.Matches.Count, Is.EqualTo(ClubCount / 2));
            Assert.That(season.CurrentRound, Is.EqualTo(1));
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
