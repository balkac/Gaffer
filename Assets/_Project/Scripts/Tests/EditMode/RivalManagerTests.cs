using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Rivals;
using Gaffer.Application.Run;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The clubs the manager is not running. Faz 3's last open deliverable, and the reason it mattered:
    /// until now the world stood still around him — the market only ever shrank when HE bought, so he had
    /// first pick of every player for ever, which is the one thing a management game cannot afford.
    /// </summary>
    public sealed class RivalManagerTests
    {
        private const ulong Seed = 20260816UL;

        private static RivalManager Rivals(RivalSettings settings = null)
        {
            return new RivalManager(settings ?? RivalSettings.Default, null);
        }

        private static Attributes Flat(byte value)
        {
            Attributes attributes = default;
            foreach (PlayerAttribute attribute in (PlayerAttribute[])Enum.GetValues(typeof(PlayerAttribute)))
            {
                attributes = attributes.WithValue(attribute, value);
            }

            return attributes;
        }

        private static Player Striker(int id, byte ability, byte potential = 70, int age = 26)
        {
            return new Player(new PlayerId(id), "P" + id, "England", PlayerRole.Striker, age, Flat(ability), potential);
        }

        private static Squad SquadOf(params Player[] players)
        {
            return new Squad(new List<Player>(players));
        }

        private static TeamStrength Strength(double value)
        {
            return new TeamStrength(value, value, value);
        }

        private static IRandom Rng()
        {
            return new SplitMix64RandomNumberGenerator(Seed);
        }

        // ----- The budget --------------------------------------------------------------------------------

        [Test]
        public void BudgetOf_AStrongerClub_HasMoreToSpendThanAWeakOne()
        {
            RivalManager rivals = Rivals();

            Assert.That(rivals.BudgetOf(Strength(70.0)), Is.GreaterThan(rivals.BudgetOf(Strength(55.0))));
        }

        [Test]
        public void BudgetOf_AClubBelowTheFloor_HasNothingToSpend()
        {
            Assert.That(Rivals().BudgetOf(Strength(40.0)), Is.EqualTo(0L));
        }

        // ----- The shortlist -----------------------------------------------------------------------------

        [Test]
        public void BuildShortlist_TakesTheMarketsBestByAbilityAndStopsAtItsSize()
        {
            var market = new List<Player>();
            for (int i = 0; i < 50; i++)
            {
                market.Add(Striker(i, (byte)(30 + i)));
            }

            List<Player> shortlist = Rivals(new RivalSettings(shortlistSize: 5)).BuildShortlist(market);

            Assert.That(shortlist.Count, Is.EqualTo(5));
            for (int i = 1; i < shortlist.Count; i++)
            {
                Assert.That(PlayerRatings.ForRole(shortlist[i]),
                    Is.LessThanOrEqualTo(PlayerRatings.ForRole(shortlist[i - 1])), "not strongest first");
            }

            Assert.That(shortlist[0].Id.Value, Is.EqualTo(49), "the best player in the market was not shortlisted");
        }

        [Test]
        public void BuildShortlist_IgnoresPotentialEntirely()
        {
            // The load-bearing design decision. Rivals buy on the number everyone can see; potential is
            // scout-masked, and seeing what they cannot is the whole discover-grow-sell fantasy. A
            // shortlist that chased ceilings would take the manager's edge away with it.
            var wonderkid = Striker(1, ability: 40, potential: 99, age: 17);
            var journeyman = Striker(2, ability: 68, potential: 68, age: 29);

            List<Player> shortlist = Rivals(new RivalSettings(shortlistSize: 1))
                .BuildShortlist(new List<Player> { wonderkid, journeyman });

            Assert.That(shortlist[0].Id, Is.EqualTo(journeyman.Id),
                "A rival went after the hidden ceiling instead of the visible ability.");
        }

        // ----- Shopping ----------------------------------------------------------------------------------

        [Test]
        public void Shop_APlayerWhoImprovesTheSquad_IsSignedAndTakenOffTheShortlist()
        {
            Squad squad = SquadOf(Striker(1, 40));
            var shortlist = new List<Player> { Striker(2, 70) };

            List<Player> signed = Rivals().Shop(squad, Strength(70.0), shortlist, Rng());

            Assert.That(signed.Count, Is.EqualTo(1));
            Assert.That(shortlist, Is.Empty, "The signed player is still available to everyone else.");
        }

        [Test]
        public void Shop_APlayerNoBetterThanWhatTheClubHas_IsLeftAlone()
        {
            // Without this a club churns its squad every summer for nothing.
            Squad squad = SquadOf(Striker(1, 70));
            var shortlist = new List<Player> { Striker(2, 70) };

            Assert.That(Rivals().Shop(squad, Strength(70.0), shortlist, Rng()), Is.Empty);
            Assert.That(shortlist.Count, Is.EqualTo(1));
        }

        [Test]
        public void Shop_AClubWithNoBudget_SignsNobodyHoweverGoodTheShortlistIs()
        {
            Squad squad = SquadOf(Striker(1, 20));
            var shortlist = new List<Player> { Striker(2, 90) };

            Assert.That(Rivals().Shop(squad, Strength(40.0), shortlist, Rng()), Is.Empty);
        }

        [Test]
        public void Shop_NeverSignsMoreThanTheSeasonAllows()
        {
            // Rivals take players off the board; they do not clear it. A market found already emptied is
            // not competition, it is a locked door.
            Squad squad = SquadOf(Striker(1, 30));
            var shortlist = new List<Player>();
            for (int i = 0; i < 20; i++)
            {
                shortlist.Add(Striker(100 + i, 75));
            }

            // The budget is made irrelevant on purpose: this pins the CAP, and a test that ran out of
            // money first would pass for the wrong reason and go on passing if the cap were removed.
            var noMoneyWorries = new RivalSettings(transferBudgetPerStrengthPoint: 100_000_000L, maxSigningsPerSeason: 2);
            List<Player> signed = Rivals(noMoneyWorries).Shop(squad, Strength(80.0), shortlist, Rng());

            Assert.That(signed.Count, Is.EqualTo(2));
        }

        // ----- Tactics -----------------------------------------------------------------------------------

        [Test]
        public void TacticsFor_ASideBetterThanItsLeague_PlaysOnTheFrontFoot()
        {
            Tactics tactics = Rivals().TacticsFor(Strength(70.0), leagueMeanStrength: 60.0);

            Assert.That(tactics.Mentality, Is.EqualTo(Mentality.Attacking));
            Assert.That(tactics.Pressing, Is.EqualTo(Pressing.Press));
        }

        [Test]
        public void TacticsFor_ASideWorseThanItsLeague_SitsInAndCounters()
        {
            // What a weaker side actually does — and what makes an upset read as an upset rather than as
            // the simulation being generous.
            Tactics tactics = Rivals().TacticsFor(Strength(50.0), leagueMeanStrength: 60.0);

            Assert.That(tactics.Mentality, Is.EqualTo(Mentality.Defensive));
            Assert.That(tactics.Approach, Is.EqualTo(Approach.Counter));
        }

        // ----- Through a run -----------------------------------------------------------------------------

        [Test]
        public void StartNextSeason_RivalsTakePlayersOffTheMarketBeforeTheManagerSeesIt()
        {
            RunSession session = StartRun();
            int before = session.GetMarket().Count;

            session.AdvanceToEndOfSeason();
            Result<SeasonRollover> rollover = session.StartNextSeason();
            Assert.That(rollover.IsSuccess, Is.True, rollover.Error);

            Assert.That(session.GetMarket().Count, Is.LessThan(before),
                "Nobody but the manager ever signs anyone — the world stands still.");
        }

        [Test]
        public void StartNextSeason_TheSameSeed_RunsTheSameRivalWindow()
        {
            RunSession first = StartRun();
            RunSession second = StartRun();

            first.AdvanceToEndOfSeason();
            second.AdvanceToEndOfSeason();
            first.StartNextSeason();
            second.StartNextSeason();

            IReadOnlyList<Player> a = first.GetMarket();
            IReadOnlyList<Player> b = second.GetMarket();
            Assert.That(b.Count, Is.EqualTo(a.Count));
            for (int i = 0; i < a.Count; i++)
            {
                Assert.That(b[i].Id, Is.EqualTo(a[i].Id), $"index {i}");
            }
        }

        private static RunSession StartRun()
        {
            var setup = new RunSetup(
                teamCount: 12, seed: Seed, managedClubIndex: 3,
                promotionPosition: 2, survivalPosition: 10,
                startingCash: 20_000_000L, weeklyWageBudget: 900_000L,
                marketSize: 120, guaranteedGems: 3);

            Result<RunSession> started = RunSessionFactory.Start(
                setup, new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }
    }
}
