using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Application.Run;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Careers move DURING a season, weighted by playing time. Until 2026-08-13 a player's ability changed
    /// exactly once a year, at the rollover, so a teenager you gave thirty games to was identical in March
    /// to what he had been in August and a reserve who never played improved just as much as your first
    /// choice. Both are now false, and these are the pins for it: the season pays development out in ticks
    /// (<see cref="PlayerDevelopment.DevelopPeriod"/>), minutes scale growth but never decline, and the
    /// pieces still add up to one season a year rather than two.
    /// </summary>
    public sealed class InSeasonDevelopmentTests
    {
        private const ulong Seed = 20260813UL;

        private static IRandom Rng(ulong seed)
        {
            return new SplitMix64RandomNumberGenerator(seed);
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

        private static Player Prospect(int id = 1, int age = 18, byte ability = 45, byte potential = 90)
        {
            return new Player(new PlayerId(id), "Prospect", "England", PlayerRole.CentralMidfield, age, Flat(ability), potential);
        }

        private static Player Veteran(int id = 1, int age = 36)
        {
            return new Player(new PlayerId(id), "Veteran", "England", PlayerRole.RightWing, age, Flat(70), 70);
        }

        // ----- The equivalence guard ---------------------------------------------------------------------

        [Test]
        public void Develop_IsAWholeDevelopPeriodPlusABirthday()
        {
            // The whole refactor rests on this: splitting a season into "move the ability" and "turn the
            // year over" must not have changed what a season IS. Same seed, same draws, same numbers.
            var development = new PlayerDevelopment();
            Player player = Prospect();

            Player throughDevelop = development.Develop(player, Rng(99UL));
            Player throughParts = development.AgeOneYear(development.DevelopPeriod(player, 1.0, 1.0, Rng(99UL)));

            Assert.That(throughParts.Age, Is.EqualTo(throughDevelop.Age));
            foreach (PlayerAttribute attribute in (PlayerAttribute[])Enum.GetValues(typeof(PlayerAttribute)))
            {
                Assert.That(throughParts.Attributes.ValueOf(attribute), Is.EqualTo(throughDevelop.Attributes.ValueOf(attribute)), attribute.ToString());
            }
        }

        [Test]
        public void DevelopPeriod_DoesNotTouchAge()
        {
            // Ageing inside the tick would give a player nine birthdays a season.
            var development = new PlayerDevelopment();
            Player player = Prospect(age: 18);

            Player after = development.DevelopPeriod(player, 0.25, 1.0, Rng(3UL));

            Assert.That(after.Age, Is.EqualTo(18));
        }

        [Test]
        public void DevelopPeriod_TenPartialTicks_DeliverAboutTheSameSeasonAsOneWholeCall()
        {
            // Growth is capped at the remaining gap and stepped probabilistically, so a season paid in ten
            // instalments cannot be bit-identical to one paid at once. What it MUST be is the same season:
            // if chunking multiplied it, every career in the game would run at ten times the pace, and if it
            // rounded it away, nobody would ever develop again. Measured across a cohort, not one player,
            // because a single probabilistic step says nothing.
            var development = new PlayerDevelopment();
            const int cohort = 200;

            double whole = 0.0;
            double chunked = 0.0;
            for (int i = 0; i < cohort; i++)
            {
                Player player = Prospect(id: i + 1);
                double start = PlayerRatings.ForRole(player);

                whole += PlayerRatings.ForRole(development.DevelopPeriod(player, 1.0, 1.0, Rng((ulong)i))) - start;

                Player stepped = player;
                for (int tick = 0; tick < 10; tick++)
                {
                    stepped = development.DevelopPeriod(stepped, 0.1, 1.0, Rng((ulong)((i * 31) + tick)));
                }

                chunked += PlayerRatings.ForRole(stepped) - start;
            }

            double wholeMean = whole / cohort;
            double chunkedMean = chunked / cohort;
            Assert.That(wholeMean, Is.GreaterThan(0.0), "The control cohort did not grow at all.");
            Assert.That(chunkedMean, Is.EqualTo(wholeMean).Within(wholeMean * 0.25),
                $"A season paid in ten ticks delivered {chunkedMean:F2} where one call delivered {wholeMean:F2}.");
        }

        // ----- Playing time ------------------------------------------------------------------------------

        [Test]
        public void DevelopPeriod_APlayerWhoPlayed_GrowsMoreThanOneWhoDidNot()
        {
            // The mechanic the owner asked for: minutes are what a young player improves on, so rotation
            // becomes a decision instead of a chore.
            var development = new PlayerDevelopment();
            Player player = Prospect();
            double start = PlayerRatings.ForRole(player);

            double starter = PlayerRatings.ForRole(development.DevelopPeriod(player, 1.0, 1.0, Rng(7UL))) - start;
            double reserve = PlayerRatings.ForRole(development.DevelopPeriod(player, 1.0, 0.45, Rng(7UL))) - start;

            Assert.That(starter, Is.GreaterThan(reserve),
                "A player who started every week developed no faster than one who never played.");
        }

        [Test]
        public void DevelopPeriod_PlayingTime_DoesNotSlowAVeteransDecline()
        {
            // A body ages whether or not it is picked. If minutes eased decline, the optimal way to keep a
            // thirty-six-year-old would be to stop playing him, which is the opposite of football.
            var development = new PlayerDevelopment();
            Player veteran = Veteran();

            Player played = development.DevelopPeriod(veteran, 1.0, 1.0, Rng(11UL));
            Player benched = development.DevelopPeriod(veteran, 1.0, 0.0, Rng(11UL));

            Assert.That(PlayerRatings.ForRole(benched), Is.EqualTo(PlayerRatings.ForRole(played)).Within(1e-9));
            Assert.That(PlayerRatings.ForRole(played), Is.LessThan(PlayerRatings.ForRole(veteran)),
                "The veteran did not decline at all, so this test is not measuring what it claims.");
        }

        // ----- Through a played season -------------------------------------------------------------------

        [Test]
        public void AdvanceToEndOfSeason_AYoungSquad_ImprovesDuringTheSeason()
        {
            // The invariant SeasonTransitionTests used to own, asserted where it now lives: against a
            // season actually being played, rather than against the summer.
            RunSession session = StartRun();
            Dictionary<int, double> before = RatingsOf(session);

            Result<IReadOnlyList<WeekOutcome>> played = session.AdvanceToEndOfSeason();
            Assert.That(played.IsSuccess, Is.True, played.Error);

            Dictionary<int, double> after = RatingsOf(session);

            int improved = 0;
            foreach (KeyValuePair<int, double> entry in before)
            {
                if (after.TryGetValue(entry.Key, out double now) && now > entry.Value)
                {
                    improved++;
                }
            }

            Assert.That(improved, Is.GreaterThan(0),
                "Not one player in the squad improved over a whole season — the development tick never fired.");
        }

        [Test]
        public void AdvanceToEndOfSeason_Market_DevelopsWithoutWaitingForTheRollover()
        {
            // The market is deferred, not frozen: reading it is what brings it up to date, and by the end
            // of a season somebody in it must have moved.
            RunSession session = StartRun();
            Dictionary<int, double> before = RatingsOf(session.GetMarket());

            Result<IReadOnlyList<WeekOutcome>> played = session.AdvanceToEndOfSeason();
            Assert.That(played.IsSuccess, Is.True, played.Error);

            Dictionary<int, double> after = RatingsOf(session.GetMarket());

            int moved = 0;
            foreach (KeyValuePair<int, double> entry in before)
            {
                if (after.TryGetValue(entry.Key, out double now) && Math.Abs(now - entry.Value) > 1e-9)
                {
                    moved++;
                }
            }

            Assert.That(moved, Is.GreaterThan(0),
                "Nobody on the market changed across a whole season — the lazy catch-up never ran.");
        }

        [Test]
        public void Market_ReadTwiceInTheSameWeek_DoesNotDevelopTwice()
        {
            // The catch-up is memoised on the round, so reading the pool is a query. If it developed per
            // read, a market screen that refreshed would age the world.
            RunSession session = StartRun();
            Result<WeekOutcome> week = session.AdvanceWeek();
            Assert.That(week.IsSuccess, Is.True, week.Error);

            Dictionary<int, double> first = RatingsOf(session.GetMarket());
            Dictionary<int, double> second = RatingsOf(session.GetMarket());

            foreach (KeyValuePair<int, double> entry in first)
            {
                Assert.That(second[entry.Key], Is.EqualTo(entry.Value).Within(1e-9), $"player {entry.Key}");
            }
        }

        // ----- The two defects this work introduced, found in review -------------------------------------

        [Test]
        public void AdvanceWeek_PastATick_DevelopsRivalClubsAndNotOnlyYours()
        {
            // Defect #1, measured before the fix: RunSession keeps its own League beside the season's, and
            // it was re-synced for the MANAGED club only — correct while that was the only roster that ever
            // changed mid-season, and wrong the moment the tick started rewriting all twenty. Every rival's
            // development was invisible here and thrown away at the rollover, so the manager's club climbed
            // (63.0 → 64.8 over six seasons) while the league it plays in sank (60.6 → 59.9).
            RunSession session = StartRun();
            ClubId rival = FirstRival(session);
            Dictionary<int, double> before = RatingsOf(session.SquadOf(rival).Players);

            for (int week = 0; week < 6; week++)
            {
                Result<WeekOutcome> played = session.AdvanceWeek();
                Assert.That(played.IsSuccess, Is.True, played.Error);
            }

            Dictionary<int, double> after = RatingsOf(session.SquadOf(rival).Players);
            Assert.That(Moved(before, after), Is.GreaterThan(0),
                "A rival club played six weeks and not one of its players changed — the league copy is stale.");
        }

        [Test]
        public void StartNextSeason_ARivalsDevelopment_SurvivesTheSummer()
        {
            // The half of defect #1 that actually cost the world its quality: the rollover reads the run's
            // League copy, so a stale one silently reverted every rival to who he had been in August.
            RunSession session = StartRun();
            ClubId rival = FirstRival(session);

            Result<IReadOnlyList<WeekOutcome>> played = session.AdvanceToEndOfSeason();
            Assert.That(played.IsSuccess, Is.True, played.Error);

            Player developed = YoungestUnder(session.SquadOf(rival).Players, 21);
            Assert.That(developed, Is.Not.Null, "No young rival to follow through the summer.");
            double ratingBeforeSummer = PlayerRatings.ForRole(developed);

            Result<SeasonRollover> rollover = session.StartNextSeason();
            Assert.That(rollover.IsSuccess, Is.True, rollover.Error);

            Player afterSummer = Find(session.SquadOf(rival).Players, developed.Id);
            Assert.That(afterSummer, Is.Not.Null, "The rival prospect is gone; pick a different subject.");
            Assert.That(afterSummer.Age, Is.EqualTo(developed.Age + 1), "The rollover is still the birthday.");
            Assert.That(PlayerRatings.ForRole(afterSummer), Is.EqualTo(ratingBeforeSummer).Within(1e-9),
                "A rival's season of development was undone by the rollover.");
        }

        [Test]
        public void Capture_OnATickBoundary_CostsNoDevelopmentAtAll()
        {
            // Defect #2, measured before the fix: development accrues in memory between ticks and none of
            // it was in the document, so a reload started the count at zero and the banked weeks vanished —
            // 0.407 squad OVR over one season for a manager who saved after every match, compounding every
            // year. Save-scumming stunted your squad. Capture now settles what it has earned before writing.
            RunSession straight = StartRun();
            for (int week = 0; week < 8; week++)
            {
                straight.AdvanceWeek();
            }

            RunSession reloaded = StartRun();
            for (int week = 0; week < 4; week++)
            {
                reloaded.AdvanceWeek();
            }

            SeasonSaveData document = reloaded.Capture();
            Result<RunSession> resumed = RunSessionFactory.Resume(SetupOf(), BalanceOf(), document, document.MatchSeed);
            Assert.That(resumed.IsSuccess, Is.True, resumed.Error);
            reloaded = resumed.Value;
            for (int week = 0; week < 4; week++)
            {
                reloaded.AdvanceWeek();
            }

            Dictionary<int, double> a = RatingsOf(straight);
            Dictionary<int, double> b = RatingsOf(reloaded);
            foreach (KeyValuePair<int, double> entry in a)
            {
                Assert.That(b.TryGetValue(entry.Key, out double mine), Is.True, $"player {entry.Key} is missing after the reload");
                Assert.That(mine, Is.EqualTo(entry.Value).Within(1e-9), $"player {entry.Key}");
            }
        }

        [Test]
        public void AdvanceToEndOfSeason_TheSameSeedAndTheSameCommands_DevelopTheSquadIdentically()
        {
            // NON-NEGOTIABLE #2 over the new path: the tick draws per player, per season, per round, so two
            // runs of the same season must produce the same careers, not merely the same scorelines.
            RunSession first = StartRun();
            RunSession second = StartRun();
            first.AdvanceToEndOfSeason();
            second.AdvanceToEndOfSeason();

            Dictionary<int, double> a = RatingsOf(first);
            Dictionary<int, double> b = RatingsOf(second);
            Assert.That(b.Count, Is.EqualTo(a.Count));
            foreach (KeyValuePair<int, double> entry in a)
            {
                Assert.That(b[entry.Key], Is.EqualTo(entry.Value).Within(1e-9), $"player {entry.Key}");
            }
        }

        [Test]
        public void SignPlayer_HoldingAReferenceFromBeforeTheMarketDeveloped_StillSignsTheLiveOne()
        {
            // Defect #3. Player is a class with no Equals override, so List.Remove matches by REFERENCE —
            // and deferred development replaces every object in the pool on catch-up. A caller holding a
            // row from before that (any UI does: it binds objects, then the weeks advance) would sign the
            // stale, weaker copy into the squad while List.Remove matched nothing and quietly left the live
            // one on the market. Neither half raises anything at the call site.
            RunSession session = StartRun();
            var before = new List<Player>(session.GetMarket());

            // Far enough in for a tick period to have reached the market, and stopping on a week the
            // window is actually open — signing is refused outside one, which would fail this test for a
            // reason that has nothing to do with what it is testing.
            while (!session.IsSeasonComplete && !(session.PlayedRounds >= DevelopmentSettings.Default.WeeksPerTick && session.IsWindowOpen))
            {
                Result<WeekOutcome> played = session.AdvanceWeek();
                Assert.That(played.IsSuccess, Is.True, played.Error);
            }

            Assert.That(session.IsWindowOpen, Is.True, "The season ended before a transfer window reopened.");

            // A tick only rebuilds a player it actually moved, and an unattached teenager's four-week
            // growth often rounds to nothing — so the subject is CHOSEN as one who did move, rather than
            // assumed. Without that the test would pass while exercising nothing.
            Player stale = null;
            Player live = null;
            for (int i = 0; i < before.Count && stale == null; i++)
            {
                Player current = Find(session.GetMarket(), before[i].Id);
                if (current != null && !ReferenceEquals(current, before[i]))
                {
                    stale = before[i];
                    live = current;
                }
            }

            Assert.That(stale, Is.Not.Null,
                "Nobody on the market developed over twelve weeks, so this test is not exercising the hazard it names.");

            Result<TransferOutcome> signing = session.SignPlayer(stale);
            Assert.That(signing.IsSuccess, Is.True, signing.Error);

            Assert.That(Find(session.GetMarket(), stale.Id), Is.Null,
                "He was signed but is still on the market — Remove matched nothing.");
            Player inSquad = Find(session.Squad.Players, stale.Id);
            Assert.That(inSquad, Is.Not.Null, "He was not added to the squad.");
            Assert.That(PlayerRatings.ForRole(inSquad), Is.EqualTo(PlayerRatings.ForRole(live)).Within(1e-9),
                "The squad got the pre-development copy rather than the player who actually exists.");
        }

        // ----- Helpers -------------------------------------------------------------------------------------

        private static ClubId FirstRival(RunSession session)
        {
            for (int club = 0; club < session.ClubCount; club++)
            {
                var id = new ClubId(club);
                if (id != session.ManagedClub && session.SquadOf(id) != null)
                {
                    return id;
                }
            }

            throw new InvalidOperationException("The league has no rival club with a squad.");
        }

        private static int Moved(Dictionary<int, double> before, Dictionary<int, double> after)
        {
            int moved = 0;
            foreach (KeyValuePair<int, double> entry in before)
            {
                if (after.TryGetValue(entry.Key, out double now) && Math.Abs(now - entry.Value) > 1e-9)
                {
                    moved++;
                }
            }

            return moved;
        }

        private static Player YoungestUnder(IReadOnlyList<Player> players, int maxAge)
        {
            Player youngest = null;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Age <= maxAge && (youngest == null || players[i].Age < youngest.Age))
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

        private static RunSetup SetupOf()
        {
            return new RunSetup(
                teamCount: 8,
                seed: Seed,
                managedClubIndex: 3,
                promotionPosition: 2,
                survivalPosition: 6,
                startingCash: 20_000_000L,
                weeklyWageBudget: 900_000L,
                marketSize: 30,
                guaranteedGems: 3);
        }

        // Drama blocks the week on an answer, which would stop these runs part-way through a season.
        private static RunBalance BalanceOf()
        {
            return new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0));
        }

        private static RunSession StartRun()
        {
            Result<RunSession> started = RunSessionFactory.Start(SetupOf(), BalanceOf());
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        private static Dictionary<int, double> RatingsOf(RunSession session)
        {
            return RatingsOf(session.Squad.Players);
        }

        private static Dictionary<int, double> RatingsOf(IReadOnlyList<Player> players)
        {
            var ratings = new Dictionary<int, double>();
            for (int i = 0; i < players.Count; i++)
            {
                ratings[players[i].Id.Value] = PlayerRatings.ForRole(players[i]);
            }

            return ratings;
        }
    }
}
