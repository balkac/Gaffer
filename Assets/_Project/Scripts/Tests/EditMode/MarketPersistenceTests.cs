using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The transfer market persists across seasons. Until 2026-08-13 it did not: the pool was discarded
    /// and regenerated every summer with a fresh id block, so a prospect you had tracked for a season did
    /// not merely fail to improve — he was deleted, along with every player you had ever sold, because a
    /// sale returns him to the pool. These are the pins that stop that from coming back, plus the id-space
    /// invariant persistence turned from an accident into a requirement (<see cref="PlayerIdSpace"/>).
    /// </summary>
    public sealed class MarketPersistenceTests
    {
        private const ulong Seed = 20260813UL;

        private static MarketRenewal Renewal()
        {
            return new MarketRenewal(new PlayerGenerator());
        }

        private static GenerationContext Ordinary()
        {
            return new GenerationContext { MinAge = 16, MaxAge = 18, MaxPotential = 60 };
        }

        // Deliberately unmistakable rather than the shipped band: the pin is the MECHANISM (the first
        // arrivals are drawn from the gem context), not today's numbers, so the two contexts are made
        // disjoint and a gem can be counted exactly.
        private static GenerationContext Gem()
        {
            return new GenerationContext { MinAge = 16, MaxAge = 18, MinPotential = 99, MaxPotential = 99 };
        }

        private static List<Player> Pool(int count, int age, int firstId = PlayerIdSpace.MarketBase)
        {
            var generator = new PlayerGenerator();
            var rng = new SplitMix64RandomNumberGenerator(Seed);
            var context = new GenerationContext { MinAge = age, MaxAge = age };
            var players = new List<Player>(count);
            for (int i = 0; i < count; i++)
            {
                players.Add(generator.Generate(new PlayerId(firstId + i), context, rng));
            }

            return players;
        }

        private static HashSet<int> IdsOf(IReadOnlyList<Player> players)
        {
            var ids = new HashSet<int>();
            for (int i = 0; i < players.Count; i++)
            {
                ids.Add(players[i].Id.Value);
            }

            return ids;
        }

        // ----- The regression pin -----------------------------------------------------------------------

        [Test]
        public void Renew_ProspectsInTheirPrime_AreTheSamePlayersAfterTheSummer()
        {
            // THE test. Regeneration passed every other assertion here — right pool size, right ages, right
            // gem count — while replacing the entire cast. Identity is the thing that was broken.
            List<Player> before = Pool(40, age: 24);
            HashSet<int> beforeIds = IdsOf(before);

            List<Player> after = Renewal().Renew(before, 40, 0, Ordinary(), Gem(), Seed, 2);

            int survivors = 0;
            for (int i = 0; i < after.Count; i++)
            {
                if (beforeIds.Contains(after[i].Id.Value))
                {
                    survivors++;
                }
            }

            Assert.That(survivors, Is.EqualTo(40),
                "A prime-age pool lost players over the summer — the market is being rebuilt, not renewed.");
        }

        [Test]
        public void Renew_EverySurvivor_IsAYearOlder()
        {
            List<Player> before = Pool(30, age: 24);
            List<Player> after = Renewal().Renew(before, 30, 0, Ordinary(), Gem(), Seed, 2);

            for (int i = 0; i < after.Count; i++)
            {
                Assert.That(after[i].Age, Is.EqualTo(25), $"Player {after[i].Id.Value} did not age.");
            }
        }

        [Test]
        public void Tick_AYoungProspect_DevelopsTowardHisCeilingWithoutBeingSigned()
        {
            // The owner's second ask: players on the market improve, they do not sit frozen waiting to be
            // bought. Development reaches them through Tick during the season — Renew only ages — so this
            // is where the claim is pinned.
            var attributes = new Attributes();
            foreach (PlayerAttribute attribute in (PlayerAttribute[])System.Enum.GetValues(typeof(PlayerAttribute)))
            {
                attributes = attributes.WithValue(attribute, 45);
            }

            var prospect = new Player(new PlayerId(PlayerIdSpace.MarketBase), "Prospect", "England", PlayerRole.Striker, 17, attributes, 92);
            double before = PlayerRatings.ForRole(prospect);

            List<Player> after = Renewal().Tick(new List<Player> { prospect }, 1.0, Seed, 2, 38);

            Assert.That(after.Count, Is.EqualTo(1));
            Assert.That(PlayerRatings.ForRole(after[0]), Is.GreaterThan(before),
                "A 17-year-old on the market with a ceiling far above him did not improve over the season.");
            Assert.That(after[0].Age, Is.EqualTo(17), "A tick is not a birthday.");
        }

        [Test]
        public void Renew_ASeasonBoundary_AgesWithoutDevelopingAgain()
        {
            // The division of labour that stops the pool getting two seasons of progress a year: the
            // rollover turns the year over and nothing else.
            List<Player> before = Pool(20, age: 20);
            List<Player> after = Renewal().Renew(before, 20, 0, Ordinary(), Gem(), Seed, 2);

            for (int i = 0; i < after.Count; i++)
            {
                Player original = Find(before, after[i].Id);
                if (original == null)
                {
                    continue;
                }

                Assert.That(after[i].Age, Is.EqualTo(original.Age + 1));
                Assert.That(PlayerRatings.ForRole(after[i]), Is.EqualTo(PlayerRatings.ForRole(original)).Within(1e-9),
                    $"Player {after[i].Id.Value} developed at the rollover as well as during the season.");
            }
        }

        // ----- Size, retirement, intake -----------------------------------------------------------------

        [Test]
        public void Renew_APoolNobodyLeaves_HoldsAtItsTargetSize()
        {
            List<Player> after = Renewal().Renew(Pool(40, age: 24), 40, 0, Ordinary(), Gem(), Seed, 2);

            Assert.That(after.Count, Is.EqualTo(40),
                "Nobody retired, so nobody should have been brought in — the pool must not ratchet upward.");
        }

        [Test]
        public void Renew_APoolShortOfTarget_IsToppedBackUp()
        {
            List<Player> after = Renewal().Renew(Pool(25, age: 24), 40, 0, Ordinary(), Gem(), Seed, 2);

            Assert.That(after.Count, Is.EqualTo(40));
        }

        [Test]
        public void Renew_PlayersPastTheHardAge_RetireOutOfThePoolAndAreReplacedByYouth()
        {
            // Everyone is past the outfield hard age, so the whole cast goes and the pool comes back to
            // target on the intake alone.
            List<Player> after = Renewal().Renew(Pool(30, age: 45), 30, 0, Ordinary(), Gem(), Seed, 2);

            Assert.That(after.Count, Is.EqualTo(30));
            for (int i = 0; i < after.Count; i++)
            {
                Assert.That(after[i].Age, Is.LessThanOrEqualTo(18),
                    "A 45-year-old survived the summer, or the intake is not drawing from the youth context.");
            }
        }

        [Test]
        public void Renew_EverySeason_BringsInTheGuaranteedGems()
        {
            // The discovery fantasy (TDD §5) used to be re-guaranteed for free, because the whole market was
            // regenerated with its gems every summer. A persistent pool has to keep seeding them or the
            // wonderkids are bought once and never replaced.
            List<Player> after = Renewal().Renew(Pool(30, age: 45), 30, 3, Ordinary(), Gem(), Seed, 2);

            int gems = 0;
            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].HiddenPotential == 99)
                {
                    gems++;
                }
            }

            Assert.That(gems, Is.EqualTo(3));
        }

        [Test]
        public void Renew_APoolAlreadyAtTarget_StillBringsInTheGuaranteedGems()
        {
            // The floor, not a slice: with no retirements the shortfall is zero, and the gems must still
            // arrive rather than being silently skipped.
            List<Player> after = Renewal().Renew(Pool(40, age: 24), 40, 2, Ordinary(), Gem(), Seed, 2);

            int gems = 0;
            for (int i = 0; i < after.Count; i++)
            {
                if (after[i].HiddenPotential == 99)
                {
                    gems++;
                }
            }

            Assert.That(gems, Is.EqualTo(2), "The guaranteed gems were dropped in a season with no retirements.");
            Assert.That(after.Count, Is.EqualTo(42), "The gem floor is expected to sit the pool that few above target.");
        }

        // ----- Determinism and identity -----------------------------------------------------------------

        [Test]
        public void Renew_SameInputs_ReproducesExactly()
        {
            List<Player> first = Renewal().Renew(Pool(40, age: 30), 40, 2, Ordinary(), Gem(), Seed, 3);
            List<Player> second = Renewal().Renew(Pool(40, age: 30), 40, 2, Ordinary(), Gem(), Seed, 3);

            Assert.That(second.Count, Is.EqualTo(first.Count));
            for (int i = 0; i < first.Count; i++)
            {
                Assert.That(second[i].Id, Is.EqualTo(first[i].Id), $"index {i}");
                Assert.That(second[i].Name, Is.EqualTo(first[i].Name), $"index {i}");
                Assert.That(second[i].Age, Is.EqualTo(first[i].Age), $"index {i}");
                Assert.That(second[i].HiddenPotential, Is.EqualTo(first[i].HiddenPotential), $"index {i}");
            }
        }

        [Test]
        public void Renew_OverManySeasons_NeverHandsOutAnIdItHasUsedBefore()
        {
            // A retiree's id must never be reissued. Faz 5's journey log is keyed by player id, so a reused
            // id would silently merge two careers into one story — the failure would surface as nonsense
            // prose long after the cause.
            MarketRenewal renewal = Renewal();
            List<Player> market = Pool(50, age: 30);
            var everSeen = new HashSet<int>(IdsOf(market));

            for (int season = 2; season <= 12; season++)
            {
                HashSet<int> before = IdsOf(market);
                market = renewal.Renew(market, 50, 1, Ordinary(), Gem(), Seed, season);
                HashSet<int> after = IdsOf(market);

                Assert.That(after.Count, Is.EqualTo(market.Count),
                    $"Season {season}: two players in the pool share an id.");

                foreach (int id in after)
                {
                    if (!before.Contains(id))
                    {
                        Assert.That(everSeen.Add(id), Is.True,
                            $"Season {season}: id {id} belonged to an earlier player and has been handed out again.");
                    }
                }
            }
        }

        // ----- The id space ------------------------------------------------------------------------------

        [Test]
        public void SeasonBase_ForEverySeason_SitsInsideMarketSpaceAndAdvances()
        {
            int previous = PlayerIdSpace.SeasonBase(0);
            Assert.That(previous, Is.EqualTo(PlayerIdSpace.MarketBase));

            for (int season = 1; season <= 1000; season++)
            {
                int current = PlayerIdSpace.SeasonBase(season);
                Assert.That(current, Is.GreaterThan(previous), $"season {season}");
                Assert.That(PlayerIdSpace.IsMarket(new PlayerId(current)), Is.True, $"season {season}");
                previous = current;
            }
        }

        [Test]
        public void SeasonBase_ASeasonNumberLargeEnoughToOverflow_StaysInsideMarketSpace()
        {
            // Unreachable in play, but the wrap would be silent and catastrophic: a negative base would
            // hand market ids to league-native players (CONVENTIONS §6).
            foreach (int season in new[] { int.MaxValue, int.MaxValue / 2, 1_000_000 })
            {
                int seasonBase = PlayerIdSpace.SeasonBase(season);
                Assert.That(seasonBase, Is.GreaterThanOrEqualTo(PlayerIdSpace.MarketBase), season.ToString());
                Assert.That(PlayerIdSpace.IsMarket(new PlayerId(seasonBase)), Is.True, season.ToString());
            }
        }

        // ----- Through the run ----------------------------------------------------------------------------

        [Test]
        public void StartNextSeason_APlayerYouSold_IsStillOnTheMarketTheNextSeason()
        {
            // The reason Faz 5 could not have been built on the old market: selling puts him back in the
            // pool, and the pool was wiped every summer, so the player whose story you most wanted to
            // follow was the one guaranteed to disappear.
            RunSession session = StartRun();
            Player sold = session.Squad.Players[session.Squad.Players.Count - 1];

            Result<TransferOutcome> sale = session.SellPlayer(sold);
            Assert.That(sale.IsSuccess, Is.True, sale.Error);

            PlayAndRollOver(session);

            Assert.That(Contains(session.Market, sold.Id), Is.True,
                "The player you sold vanished over the summer.");
        }

        [Test]
        public void StartNextSeason_TheProspectYouWereWatching_IsStillThereADevelopedYearOlder()
        {
            RunSession session = StartRun();
            Player watched = Youngest(session.Market);
            int ageBefore = watched.Age;

            PlayAndRollOver(session);

            Player found = Find(session.Market, watched.Id);
            Assert.That(found, Is.Not.Null, "The prospect you were tracking was deleted over the summer.");
            Assert.That(found.Age, Is.EqualTo(ageBefore + 1));
        }

        [Test]
        public void StartNextSeason_AfterSigningFromTheMarket_LeagueIntakeStaysOutOfMarketIdSpace()
        {
            // The collision persistence created: a signed free agent carries a market id into a squad, and
            // the academy allocates "one past the highest id here". Without the league/market partition the
            // next youth would be born inside the block the market is about to hand out.
            RunSession session = StartRun();
            Player target = Youngest(session.Market);
            Result<TransferOutcome> signing = session.SignPlayer(target);
            Assert.That(signing.IsSuccess, Is.True, signing.Error);
            Assert.That(PlayerIdSpace.IsMarket(target.Id), Is.True, "The market is expected to allocate above the base.");

            PlayAndRollOver(session);

            var marketIds = IdsOf(session.Market);
            for (int club = 0; club < session.ClubCount; club++)
            {
                Squad squad = session.SquadOf(new ClubId(club));
                if (squad == null)
                {
                    continue;
                }

                IReadOnlyList<Player> players = squad.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    PlayerId id = players[i].Id;
                    if (id == target.Id)
                    {
                        continue;
                    }

                    Assert.That(marketIds.Contains(id.Value), Is.False,
                        $"Squad player {id.Value} shares an id with someone on the market.");
                }
            }
        }

        // ----- Run helpers --------------------------------------------------------------------------------

        private static RunSession StartRun()
        {
            var setup = new RunSetup(
                teamCount: 8,
                seed: Seed,
                managedClubIndex: 3,
                promotionPosition: 2,
                survivalPosition: 6,
                startingCash: 20_000_000L,
                weeklyWageBudget: 900_000L,
                marketSize: 24,
                guaranteedGems: 2);

            Result<RunSession> started = RunSessionFactory.Start(setup, new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0)));
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static void PlayAndRollOver(RunSession session)
        {
            Result<IReadOnlyList<WeekOutcome>> played = session.AdvanceToEndOfSeason();
            Assert.That(played.IsSuccess, Is.True, played.Error);

            Result<SeasonRollover> rollover = session.StartNextSeason();
            Assert.That(rollover.IsSuccess, Is.True, rollover.Error);
        }

        private static Player Youngest(IReadOnlyList<Player> players)
        {
            Player youngest = players[0];
            for (int i = 1; i < players.Count; i++)
            {
                if (players[i].Age < youngest.Age)
                {
                    youngest = players[i];
                }
            }

            return youngest;
        }

        private static Player Find(IReadOnlyList<Player> players, PlayerId id)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == id)
                {
                    return players[i];
                }
            }

            return null;
        }

        private static bool Contains(IReadOnlyList<Player> players, PlayerId id)
        {
            return Find(players, id) != null;
        }
    }
}
