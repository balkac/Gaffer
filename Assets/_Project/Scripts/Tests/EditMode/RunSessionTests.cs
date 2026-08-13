using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
using Gaffer.UserData;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The run flow, headless. This was the one part of the game with no test at all — it lived twice
    /// inside two editor windows, which is exactly why the two copies drifted (one paid wages and ticked
    /// drama every week, the other did neither). The pins below are the ones that would have caught the
    /// drift: the week loop reproduces a season driven straight through <see cref="LeagueSeason"/>, wages
    /// really leave the cash every week, a drama resolution lands morale, cash and the sale in one
    /// ordered step (and lands none of them when the sale cannot go through), the rollover develops the
    /// world, and the same seed replays the same season.
    /// </summary>
    public sealed class RunSessionTests
    {
        private const int ClubCount = 8;
        private const int ManagedIndex = 5;
        private const ulong Seed = 20260806UL;

        // Drama demands an answer and blocks the week, so the tests that measure the *season* silence it
        // and the tests that measure *drama* force it. Nothing in between is deterministic enough to pin.
        private static RunBalance QuietDrama()
        {
            return new RunBalance(drama: new DramaSettings(maxEventsPerSeason: 0));
        }

        // Drama silenced (it blocks the week on an answer) and the development tick pushed past any window
        // these tests play, so the run's loop can be compared against a bare one.
        private static RunBalance NoDevelopment()
        {
            return new RunBalance(
                drama: new DramaSettings(maxEventsPerSeason: 0),
                development: new DevelopmentSettings(weeksPerTick: 1000));
        }

        private static RunSetup Setup()
        {
            return new RunSetup(
                teamCount: ClubCount,
                seed: Seed,
                managedClubIndex: ManagedIndex,
                promotionPosition: 2,
                survivalPosition: 6,
                startingCash: 6_000_000L,
                weeklyWageBudget: 400_000L,
                marketSize: 12,
                guaranteedGems: 2);
        }

        private static RunSession StartRun(RunSetup setup, RunBalance balance)
        {
            Result<RunSession> started = RunSessionFactory.Start(setup, balance);
            Assert.That(started.IsSuccess, Is.True, started.Error);
            return started.Value;
        }

        // ----- The equivalence pin ----------------------------------------------------------------------

        [Test]
        public void AdvanceWeek_DrivenThroughTheSession_ReproducesTheSeasonDrivenThroughLeagueSeason()
        {
            const int weeks = 6;

            // Development is silenced for this pin, not forgotten. The run does MORE than LeagueSeason's
            // week loop now — every WeeksPerTick weeks it develops all twenty rosters, which changes the
            // strengths the next round is played on — so a bare season would legitimately diverge from
            // round five and the pin would be asserting something false. Pushing the tick past the window
            // keeps this test measuring what it was written to measure: that the ORDER of the week loop is
            // the same one, not that the run has stopped doing anything else.
            RunSession session = StartRun(Setup(), NoDevelopment());

            var throughSession = new List<MatchResult>();
            for (int week = 0; week < weeks; week++)
            {
                Result<WeekOutcome> outcome = session.AdvanceWeek();
                Assert.That(outcome.IsSuccess, Is.True, outcome.Error);
                throughSession.AddRange(outcome.Value.Matches);
            }

            IReadOnlyList<MatchResult> direct = PlayedDirectly(weeks, session.SeasonMatchSeed, RivalriesOf(session));

            Assert.That(throughSession.Count, Is.EqualTo(direct.Count));
            for (int i = 0; i < direct.Count; i++)
            {
                MatchResult a = throughSession[i];
                MatchResult b = direct[i];
                string where = "match " + i;
                Assert.That(a.Home.Value, Is.EqualTo(b.Home.Value), where);
                Assert.That(a.Away.Value, Is.EqualTo(b.Away.Value), where);
                Assert.That(a.HomeGoals, Is.EqualTo(b.HomeGoals), where);
                Assert.That(a.AwayGoals, Is.EqualTo(b.AwayGoals), where);
                Assert.That(a.HomeShots, Is.EqualTo(b.HomeShots), where);
                Assert.That(a.AwayShots, Is.EqualTo(b.AwayShots), where);
            }
        }

        // The same world and the same weekly loop, wired by hand the way the editor windows used to wire
        // it. If RunSession ever stops being a pure re-expression of this, this test says so.
        // The rivalry map the run drew for itself, rebuilt through the public read model. A season played
        // without it raises no fixture into a derby, and the two loops would diverge for a reason that has
        // nothing to do with what this test is pinning.
        private static RivalryTable RivalriesOf(RunSession session)
        {
            var rivalOf = new int[session.ClubCount];
            for (int club = 0; club < session.ClubCount; club++)
            {
                rivalOf[club] = session.RivalOf(new ClubId(club)).Value;
            }

            return RivalryTable.Restore(rivalOf);
        }

        private static IReadOnlyList<MatchResult> PlayedDirectly(int weeks, ulong matchSeed, RivalryTable rivalries)
        {
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator(TraitCatalog.Default)), TraitCatalog.Default);
            League league = generator.Generate(ClubCount, new SplitMix64RandomNumberGenerator(Seed ^ 0x5EEDD5EEDUL));

            var simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver(),
                new WeightedScorerSelector(ScorerWeights.Default));

            var season = new LeagueSeason(league, TraitCatalog.Default, TacticsSettings.Default, MoraleSettings.Default, simulator);
            season.SetMatchContextBuilder(new MatchContextBuilder(rivalries, MatchContextSettings.Default));
            var managed = new ClubId(ManagedIndex);
            season.SetFormation(managed, Formation.F442);
            season.SetTactics(managed, Tactics.Balanced);
            season.SetStarters(managed, new List<Player>(new LineupSelector().SelectBest(season.SquadOf(managed), Formation.F442)));

            MatchContext context = RunSetup.Default.MatchContext;
            for (int week = 0; week < weeks; week++)
            {
                season.AdvanceWeek(context, matchSeed);
            }

            return season.PlayedResults;
        }

        [Test]
        public void AdvanceWeek_SameSeed_ReplaysTheWholeSeasonExactly()
        {
            RunSession first = StartRun(Setup(), QuietDrama());
            RunSession second = StartRun(Setup(), QuietDrama());

            Result<IReadOnlyList<WeekOutcome>> a = first.AdvanceToEndOfSeason();
            Result<IReadOnlyList<WeekOutcome>> b = second.AdvanceToEndOfSeason();

            Assert.That(a.IsSuccess, Is.True, a.Error);
            Assert.That(b.IsSuccess, Is.True, b.Error);
            Assert.That(first.IsSeasonComplete, Is.True);
            Assert.That(a.Value.Count, Is.EqualTo(first.RoundCount));
            Assert.That(b.Value.Count, Is.EqualTo(a.Value.Count));

            for (int week = 0; week < a.Value.Count; week++)
            {
                WeekOutcome left = a.Value[week];
                WeekOutcome right = b.Value[week];
                Assert.That(right.Round, Is.EqualTo(left.Round));
                Assert.That(right.TablePosition, Is.EqualTo(left.TablePosition), "week " + week);
                Assert.That(right.LossStreak, Is.EqualTo(left.LossStreak), "week " + week);
                Assert.That(right.Finances.Cash, Is.EqualTo(left.Finances.Cash), "week " + week);
                Assert.That(right.Matches.Count, Is.EqualTo(left.Matches.Count));
                for (int i = 0; i < left.Matches.Count; i++)
                {
                    Assert.That(right.Matches[i].HomeGoals, Is.EqualTo(left.Matches[i].HomeGoals));
                    Assert.That(right.Matches[i].AwayGoals, Is.EqualTo(left.Matches[i].AwayGoals));
                }
            }

            Assert.That(second.Verdict, Is.EqualTo(first.Verdict));
            Assert.That(second.TablePosition, Is.EqualTo(first.TablePosition));
        }

        // ----- What the view used to derive for itself --------------------------------------------------

        [Test]
        public void AdvanceWeek_Outcome_CarriesThePositionAndStreakTheViewUsedToScanFor()
        {
            RunSession session = StartRun(Setup(), QuietDrama());

            var weeks = new List<WeekOutcome>();
            for (int week = 0; week < 5; week++)
            {
                weeks.Add(session.AdvanceWeek().Value);
            }

            WeekOutcome outcome = weeks[weeks.Count - 1];

            Assert.That(outcome.TablePosition, Is.InRange(1, ClubCount));
            Assert.That(outcome.TablePosition, Is.EqualTo(PositionInStandings(session)),
                "the position in the record is the position in the table");
            Assert.That(outcome.LossStreak, Is.EqualTo(StreakAcross(weeks, session.ManagedClub)),
                "the streak in the record is what replaying the weeks says it is");
            Assert.That(outcome.ManagedMatch, Is.Not.Null, "the managed club plays every round of a round-robin");
            Assert.That(outcome.PlayedRounds, Is.EqualTo(5));
            Assert.That(outcome.RoundCount, Is.EqualTo(2 * (ClubCount - 1)));
            Assert.That(outcome.WindowPhase, Is.EqualTo(TransferWindowPhase.Closed), "the summer window shut at kickoff");
            Assert.That(outcome.Verdict, Is.Null, "there is a season left to play");
        }

        private static int PositionInStandings(RunSession session)
        {
            IReadOnlyList<LeagueTableRow> rows = session.Standings();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Club == session.ManagedClub)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        // The streak derived from the replayed outcomes alone — the view's job now, and an independent
        // check on the derivation that moved out of it.
        private static int StreakAcross(IReadOnlyList<WeekOutcome> weeks, ClubId managed)
        {
            int streak = 0;
            for (int i = weeks.Count - 1; i >= 0; i--)
            {
                MatchResult? played = weeks[i].ManagedMatch;
                if (played == null)
                {
                    continue;
                }

                MatchResult match = played.Value;
                bool home = match.Home == managed;
                bool lost = home ? match.HomeGoals < match.AwayGoals : match.AwayGoals < match.HomeGoals;
                if (!lost)
                {
                    break;
                }

                streak++;
            }

            return streak;
        }

        [Test]
        public void AdvanceWeek_WhileADramaIsPending_IsRefusedUntilItIsAnswered()
        {
            RunSession session = StartRun(Setup(), AlwaysFire(SellingEvent()));

            Assert.That(session.AdvanceWeek().IsSuccess, Is.True);
            Assert.That(session.PendingDrama, Is.Not.Null, "the forced catalog fires every week");

            Result<WeekOutcome> blocked = session.AdvanceWeek();

            Assert.That(blocked.IsFailure, Is.True, "drama is a decision, not a notification");
            Assert.That(session.PlayedRounds, Is.EqualTo(1), "and the week did not slip past");
        }

        // ----- Wages ------------------------------------------------------------------------------------

        [Test]
        public void AdvanceWeek_EveryWeek_TakesTheWageBillOutOfTheTransferCash()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            long cashAtKickoff = session.Finances.Cash;
            long weeklyBill = session.Finances.WeeklyWageBill;

            Assert.That(weeklyBill, Is.GreaterThan(0), "a generated squad costs something to field");

            for (int week = 1; week <= 4; week++)
            {
                WeekOutcome outcome = session.AdvanceWeek().Value;

                Assert.That(outcome.WagesPaid, Is.EqualTo(weeklyBill), "week " + week);
                Assert.That(outcome.Finances.Cash, Is.EqualTo(cashAtKickoff - (weeklyBill * week)), "week " + week);
                Assert.That(session.Finances.Cash, Is.EqualTo(outcome.Finances.Cash));
            }
        }

        // ----- Drama, applied in one ordered step -------------------------------------------------------

        private static RunBalance AlwaysFire(DramaEvent forced)
        {
            return new RunBalance(
                dramaEvents: new DramaCatalog(new[] { forced }),
                drama: new DramaSettings(
                    maxEventsPerSeason: 99,
                    minWeeksBetweenEvents: 1,
                    weeklyChancePerWeight: 1.0,
                    maxWeeklyChance: 1.0));
        }

        // Choice 0 does all three things at once — that is the point: morale, cash and a forced sale are
        // one transaction. Choice 1 does none of them, so a rejected sale still leaves a way out.
        private static DramaEvent SellingEvent()
        {
            return new DramaEvent(
                new DramaEventId("test-sell-demand"), DramaCategory.Personal,
                "drama.test.title", "drama.test.body",
                requiresSubject: true,
                new DramaTrigger(),
                baseWeight: 1.0, cooldownWeeks: 0,
                new[]
                {
                    new DramaChoice("drama.test.let_him_go", new[]
                    {
                        new DramaEffect(DramaEffectKind.SubjectMorale, -3.0, 4),
                        new DramaEffect(DramaEffectKind.Cash, -250_000.0),
                        new DramaEffect(DramaEffectKind.SellSubject),
                    }),
                    new DramaChoice("drama.test.keep_him", new[]
                    {
                        new DramaEffect(DramaEffectKind.TeamMorale, 1.0, 2),
                    }),
                });
        }

        [Test]
        public void ResolveDrama_SellChoice_AppliesMoraleCashAndTheSaleInOneStep()
        {
            RunSession session = StartRun(Setup(), AlwaysFire(SellingEvent()));
            session.AdvanceWeek();

            PendingDrama pending = session.PendingDrama;
            Assert.That(pending, Is.Not.Null);
            Player subject = pending.Subject;
            long cashBefore = session.Finances.Cash;
            long wageBefore = session.Finances.WeeklyWageBill;
            long fee = session.FeeOf(subject);
            long wage = session.WeeklyWageOf(subject);
            int squadBefore = session.Squad.Count;

            Result<DramaResolution> resolved = session.ResolveDrama(0);

            Assert.That(resolved.IsSuccess, Is.True, resolved.Error);
            DramaResolution outcome = resolved.Value;

            // Everything it changed, in the record — a UI can replay all of it.
            Assert.That(outcome.CashDelta, Is.EqualTo(-250_000));
            Assert.That(outcome.SoldPlayer, Is.SameAs(subject));
            Assert.That(outcome.SaleFee, Is.EqualTo(fee));
            Assert.That(outcome.MoraleChanges.Count, Is.EqualTo(1));
            Assert.That(outcome.MoraleChanges[0].Player, Is.EqualTo(subject.Id));
            Assert.That(outcome.MoraleChanges[0].Points, Is.EqualTo(-3.0).Within(1e-9));

            // And in the run: morale landed on the ledger, cash moved twice, the roster shrank, the sold
            // player is back on the market, and the eleven was re-picked without him.
            Assert.That(session.MoralePointsOf(subject.Id), Is.EqualTo(-3.0).Within(1e-9));
            Assert.That(session.Finances.Cash, Is.EqualTo(cashBefore - 250_000 + fee));
            Assert.That(outcome.Finances.Cash, Is.EqualTo(session.Finances.Cash));
            Assert.That(session.Finances.WeeklyWageBill, Is.EqualTo(wageBefore - wage));
            Assert.That(session.Squad.Count, Is.EqualTo(squadBefore - 1));
            Assert.That(session.Squad.Contains(subject.Id), Is.False);
            Assert.That(session.GetMarket(), Contains.Item(subject));
            Assert.That(outcome.Lineup.Starters, Has.No.Member(subject));
            Assert.That(session.PendingDrama, Is.Null, "the event is answered");
        }

        [Test]
        public void ResolveDrama_SaleTheSquadCannotMake_AppliesNothingAndLeavesTheEventPending()
        {
            RunSession session = StartRun(Setup(), AlwaysFire(SellingEvent()));

            // Sell down to the minimum squad in the summer window, so the forced sale is refused.
            SellDownToMinimum(session);
            long cashBefore = session.Finances.Cash;
            int squadBefore = session.Squad.Count;

            session.AdvanceWeek();
            PendingDrama pending = session.PendingDrama;
            Assert.That(pending, Is.Not.Null);
            Player subject = pending.Subject;
            long cashAfterWages = session.Finances.Cash;

            Result<DramaResolution> rejected = session.ResolveDrama(0);

            Assert.That(rejected.IsFailure, Is.True, "the whole resolution is rejected, not half-applied");
            Assert.That(session.MoralePointsOf(subject.Id), Is.EqualTo(0.0).Within(1e-9), "no morale landed");
            Assert.That(session.Finances.Cash, Is.EqualTo(cashAfterWages), "no cash moved");
            Assert.That(session.Squad.Count, Is.EqualTo(squadBefore), "no one left");
            Assert.That(session.PendingDrama, Is.SameAs(pending), "the decision is still yours to make");
            Assert.That(cashBefore, Is.GreaterThan(0), "the run was solvent before the week's wages");

            // The other answer still works, so a refused sale never deadlocks the run.
            Result<DramaResolution> answered = session.ResolveDrama(1);
            Assert.That(answered.IsSuccess, Is.True, answered.Error);
            Assert.That(session.PendingDrama, Is.Null);
            Assert.That(session.MoralePointsOf(subject.Id), Is.EqualTo(1.0).Within(1e-9), "the room lifts instead");
        }

        private static void SellDownToMinimum(RunSession session)
        {
            Assert.That(session.IsWindowOpen, Is.True, "the summer window is open before kickoff");

            int guard = 0;
            while (guard++ < 64)
            {
                IReadOnlyList<Player> players = session.Squad.Players;
                Result<TransferOutcome> sold = session.SellPlayer(players[players.Count - 1]);
                if (sold.IsFailure)
                {
                    break;
                }
            }

            Assert.That(session.Squad.Count, Is.EqualTo(11), "a squad may not be sold below the eleven");
        }

        [Test]
        public void ResolveDrama_WithNothingPending_FailsWithAResult()
        {
            RunSession session = StartRun(Setup(), QuietDrama());

            Result<DramaResolution> resolved = session.ResolveDrama(0);

            Assert.That(resolved.IsFailure, Is.True);
        }

        // ----- Transfers --------------------------------------------------------------------------------

        [Test]
        public void SignPlayer_InAnOpenWindow_PaysTheFeeAndRepicksTheEleven()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            Player target = MostAffordable(session);
            long cashBefore = session.Finances.Cash;
            long wageBefore = session.Finances.WeeklyWageBill;
            int squadBefore = session.Squad.Count;

            Result<TransferOutcome> signed = session.SignPlayer(target);

            Assert.That(signed.IsSuccess, Is.True, signed.Error);
            Assert.That(signed.Value.Fee, Is.EqualTo(session.ValueOf(target)));
            Assert.That(session.Finances.Cash, Is.EqualTo(cashBefore - signed.Value.Fee));
            Assert.That(session.Finances.WeeklyWageBill, Is.EqualTo(wageBefore + signed.Value.WeeklyWage));
            Assert.That(session.Squad.Count, Is.EqualTo(squadBefore + 1));
            Assert.That(session.GetMarket(), Has.No.Member(target));
            Assert.That(signed.Value.Lineup.Starters.Count, Is.EqualTo(session.Formation.Total));
        }

        [Test]
        public void SignPlayer_WithTheWindowShut_IsRefused()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            Player target = MostAffordable(session);
            session.AdvanceWeek();

            Result<TransferOutcome> signed = session.SignPlayer(target);

            Assert.That(signed.IsFailure, Is.True);
            Assert.That(session.GetMarket(), Contains.Item(target));
        }

        private static Player MostAffordable(RunSession session)
        {
            Player cheapest = null;
            foreach (Player player in session.GetMarket())
            {
                if (cheapest == null || session.FeeOf(player) < session.FeeOf(cheapest))
                {
                    cheapest = player;
                }
            }

            Assert.That(cheapest, Is.Not.Null, "the market is generated with the run");
            return cheapest;
        }

        // ----- The budget exchange ------------------------------------------------------------------------

        [Test]
        public void ShiftWageBudget_GivingUpWageRoom_SurfacesTheNewFinancesInTheOutcome()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            Finances before = session.Finances;
            long weeks = session.WageBudgetExchangeWeeks;
            Assert.That(before.WageHeadroom, Is.GreaterThan(1_000), "the run must start with room to give up");

            Result<BudgetShiftOutcome> result = session.ShiftWageBudget(-1_000);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            BudgetShiftOutcome outcome = result.Value;

            Assert.That(outcome.WeeklyWageBudgetDelta, Is.EqualTo(-1_000));
            Assert.That(outcome.CashDelta, Is.EqualTo(1_000 * weeks));
            Assert.That(outcome.Finances.Cash, Is.EqualTo(before.Cash + (1_000 * weeks)));
            Assert.That(outcome.Finances.WeeklyWageBudget, Is.EqualTo(before.WeeklyWageBudget - 1_000));
            Assert.That(outcome.Finances.WeeklyWageBill, Is.EqualTo(before.WeeklyWageBill), "no player moved");

            // The view replays the outcome instead of re-reading the session, so the two must agree.
            Assert.That(session.Finances.Cash, Is.EqualTo(outcome.Finances.Cash));
            Assert.That(session.Finances.WeeklyWageBudget, Is.EqualTo(outcome.Finances.WeeklyWageBudget));
        }

        [Test]
        public void ShiftWageBudget_BuyingWageRoom_SpendsCashAndWidensTheHeadroom()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            Finances before = session.Finances;
            long weeks = session.WageBudgetExchangeWeeks;

            Result<BudgetShiftOutcome> result = session.ShiftWageBudget(2_000);

            Assert.That(result.IsSuccess, Is.True, result.Error);
            Assert.That(result.Value.CashDelta, Is.EqualTo(-2_000 * weeks));
            Assert.That(result.Value.Finances.WageHeadroom, Is.EqualTo(before.WageHeadroom + 2_000));
        }

        [Test]
        public void ShiftWageBudget_WithTheTransferWindowShut_IsStillAllowed()
        {
            // Unlike SignPlayer, which the same state refuses one test above: the exchange moves no player,
            // and being told to wait for the window is exactly the stuck the owner complained about.
            RunSession session = StartRun(Setup(), QuietDrama());
            session.AdvanceWeek();
            Assert.That(session.IsWindowOpen, Is.False, "the window shuts once the season is under way");

            Result<BudgetShiftOutcome> result = session.ShiftWageBudget(-1_000);

            Assert.That(result.IsSuccess, Is.True, result.Error);
        }

        [Test]
        public void ShiftWageBudget_MoreThanTheUncommittedCeiling_IsRefusedAndMovesNothing()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            Finances before = session.Finances;

            Result<BudgetShiftOutcome> result = session.ShiftWageBudget(-(before.WageHeadroom + 1));

            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Does.Contain("wage ceiling"), "the refusal names the limit that blocked it");
            Assert.That(session.Finances.Cash, Is.EqualTo(before.Cash), "a refused shift is not a partial one");
            Assert.That(session.Finances.WeeklyWageBudget, Is.EqualTo(before.WeeklyWageBudget));
        }

        [Test]
        public void PreviewWageBudgetShift_BeforeTheCommand_AnswersExactlyWhatTheCommandWill()
        {
            // What the windows draw under the buttons. It must be the command's own answer, or the preview
            // is a second implementation of the rule waiting to drift from it.
            RunSession session = StartRun(Setup(), QuietDrama());
            Finances before = session.Finances;
            long tooMuch = before.WageHeadroom + 1;

            // The refused direction: same words, so the line under a dead button is the message the click
            // would print.
            Result<Finances> previewBad = session.PreviewWageBudgetShift(-tooMuch);
            Assert.That(previewBad.IsFailure, Is.True);
            Assert.That(session.Finances.WeeklyWageBudget, Is.EqualTo(before.WeeklyWageBudget), "a preview commits nothing");
            Assert.That(session.ShiftWageBudget(-tooMuch).Error, Is.EqualTo(previewBad.Error));

            // And the allowed one: same figures.
            Result<Finances> previewOk = session.PreviewWageBudgetShift(-1_000);
            Assert.That(previewOk.IsSuccess, Is.True, previewOk.Error);

            Result<BudgetShiftOutcome> done = session.ShiftWageBudget(-1_000);
            Assert.That(done.IsSuccess, Is.True, done.Error);
            Assert.That(done.Value.Finances.Cash, Is.EqualTo(previewOk.Value.Cash));
            Assert.That(done.Value.Finances.WeeklyWageBudget, Is.EqualTo(previewOk.Value.WeeklyWageBudget));
        }

        // ----- Lineup -----------------------------------------------------------------------------------

        [Test]
        public void ToggleStarter_WithAFullEleven_RefusesAndLeavesTheSheetAlone()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            LineupOutcome sheet = session.Lineup();
            Assert.That(sheet.IsComplete, Is.True);

            Player benched = sheet.Bench[0];
            Result<LineupOutcome> refused = session.ToggleStarter(benched.Id);

            Assert.That(refused.IsFailure, Is.True);
            Assert.That(session.Lineup().Starters.Count, Is.EqualTo(sheet.Starters.Count));
        }

        [Test]
        public void ToggleStarter_ThenPlaceInSlot_MovesThePlayerOntoTheSheet()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            LineupOutcome sheet = session.Lineup();
            Player dropped = sheet.Starters[10];
            Player promoted = sheet.Bench[0];

            Assert.That(session.ToggleStarter(dropped.Id).IsSuccess, Is.True);
            Result<LineupOutcome> placed = session.PlaceInSlot(10, promoted.Id);

            Assert.That(placed.IsSuccess, Is.True, placed.Error);
            Assert.That(placed.Value.Slots[10], Is.SameAs(promoted));
            Assert.That(placed.Value.IsComplete, Is.True);
            Assert.That(placed.Value.Bench, Contains.Item(dropped));
        }

        [Test]
        public void SetTactics_Attacking_ChangesTheStrengthTheSheetReports()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            LineupOutcome balanced = session.Lineup();

            Result<LineupOutcome> attacking = session.SetTactics(
                new Tactics(Mentality.VeryAttacking, Tempo.Intense, Pressing.Press, Approach.Counter));

            Assert.That(attacking.IsSuccess, Is.True);
            Assert.That(attacking.Value.Strength.Attack, Is.GreaterThan(balanced.Strength.Attack));
            Assert.That(attacking.Value.ChanceProfile.Quality, Is.GreaterThan(balanced.ChanceProfile.Quality),
                "the counter makes fewer but sharper chances");
        }

        // ----- The rollover -----------------------------------------------------------------------------

        [Test]
        public void StartNextSeason_AfterTheFinalRound_DevelopsTheWorldAndOpensAFreshSeason()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            session.AdvanceToEndOfSeason();
            Assert.That(session.IsSeasonComplete, Is.True);

            long cashAtSeasonEnd = session.Finances.Cash;
            int squadBefore = session.Squad.Count;

            Result<SeasonRollover> rolled = session.StartNextSeason();

            Assert.That(rolled.IsSuccess, Is.True, rolled.Error);
            SeasonRollover rollover = rolled.Value;

            Assert.That(rollover.SeasonNumber, Is.EqualTo(2));
            Assert.That(session.SeasonNumber, Is.EqualTo(2));
            Assert.That(session.PlayedRounds, Is.EqualTo(0), "a fresh fixture list");
            Assert.That(session.Verdict, Is.Null, "last year's verdict is spent");
            Assert.That(session.IsWindowOpen, Is.True, "the summer window is open again");
            Assert.That(rollover.Finances.Cash, Is.EqualTo(cashAtSeasonEnd), "cash carries over");
            Assert.That(rollover.Finances.WeeklyWageBill, Is.GreaterThan(0), "the bill is re-derived from the developed squad");
            Assert.That(rollover.Market.Count, Is.GreaterThan(0));
            Assert.That(rollover.Lineup.IsComplete, Is.True, "the eleven is re-picked from the developed roster");

            foreach (Player arrival in rollover.Arrived)
            {
                Assert.That(session.Squad.Contains(arrival.Id), Is.True, "an arrival is in the squad");
            }

            foreach (Player gone in rollover.Retired)
            {
                Assert.That(session.Squad.Contains(gone.Id), Is.False, "a retirement is not");
            }

            Assert.That(squadBefore, Is.GreaterThan(0));
            Assert.That(session.Squad.Count, Is.EqualTo(squadBefore - rollover.Retired.Count + rollover.Arrived.Count),
                "the summer diff accounts for every change to the roster");
        }

        [Test]
        public void StartNextSeason_AfterGivingUpWageRoom_DoesNotHandTheCeilingBack()
        {
            // The exchange would otherwise be a money printer on a one-season timer: give up ceiling for
            // cash, keep the cash, and let the summer re-seed the ceiling from the run's setup. A slice
            // sold stays sold, so the ceiling carries over as it stands.
            RunSession session = StartRun(Setup(), QuietDrama());
            long ceilingAtKickoff = session.Finances.WeeklyWageBudget;

            Result<BudgetShiftOutcome> sold = session.ShiftWageBudget(-10_000);
            Assert.That(sold.IsSuccess, Is.True, sold.Error);

            session.AdvanceToEndOfSeason();
            Result<SeasonRollover> rolled = session.StartNextSeason();

            Assert.That(rolled.IsSuccess, Is.True, rolled.Error);
            Assert.That(rolled.Value.Finances.WeeklyWageBudget, Is.EqualTo(ceilingAtKickoff - 10_000));
            Assert.That(session.Finances.WeeklyWageBudget, Is.EqualTo(ceilingAtKickoff - 10_000));
        }

        [Test]
        public void StartNextSeason_MidSeason_IsRefused()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            session.AdvanceWeek();

            Result<SeasonRollover> rolled = session.StartNextSeason();

            Assert.That(rolled.IsFailure, Is.True);
            Assert.That(session.SeasonNumber, Is.EqualTo(1));
        }

        // ----- Save / resume ----------------------------------------------------------------------------

        private static RunSession Resume(RunSetup setup, RunBalance balance, SeasonSaveData saved, ulong continuationSeed)
        {
            Result<RunSession> resumed = RunSessionFactory.Resume(setup, balance, saved, continuationSeed);
            Assert.That(resumed.IsSuccess, Is.True, resumed.Error);
            return resumed.Value;
        }

        [Test]
        public void Resume_FromACapturedRun_ContinuesTheSameFixturesExactly()
        {
            RunSession played = StartRun(Setup(), QuietDrama());
            for (int week = 0; week < 4; week++)
            {
                played.AdvanceWeek();
            }

            SeasonSaveData saved = played.Capture();

            // The save's own seed as the continuation seed — which is what makes this the DETERMINISM pin
            // it has always been: the core is a pure function of the seed it is given, and given the same
            // one it reproduces the run exactly. The game does not pass this seed (it passes a fresh one,
            // see the test below); a test does, and that is the whole point of the argument.
            RunSession resumed = Resume(Setup(), QuietDrama(), saved, saved.MatchSeed);

            Assert.That(resumed.PlayedRounds, Is.EqualTo(played.PlayedRounds));
            Assert.That(resumed.SeasonNumber, Is.EqualTo(played.SeasonNumber));

            WeekOutcome next = played.AdvanceWeek().Value;
            WeekOutcome resumedNext = resumed.AdvanceWeek().Value;

            Assert.That(resumedNext.Matches.Count, Is.EqualTo(next.Matches.Count));
            for (int i = 0; i < next.Matches.Count; i++)
            {
                Assert.That(resumedNext.Matches[i].HomeGoals, Is.EqualTo(next.Matches[i].HomeGoals), "match " + i);
                Assert.That(resumedNext.Matches[i].AwayGoals, Is.EqualTo(next.Matches[i].AwayGoals), "match " + i);
            }
        }

        [Test]
        public void Resume_OnADifferentContinuationSeed_ReplaysTheHistoryAndChangesTheFuture()
        {
            // The counterpart to the test above, and the owner's actual complaint: advancing from a save
            // always produced the same scorelines. It no longer has to — the caller chooses what the
            // unplayed fixtures are seeded from, so the game hands over a session-fresh number and the same
            // eleven can lose the match it won an hour ago. What must NOT move is the history.
            RunSession played = StartRun(Setup(), QuietDrama());
            for (int week = 0; week < 4; week++)
            {
                played.AdvanceWeek();
            }

            SeasonSaveData saved = played.Capture();
            string historyAtSave = TableSignature(played);

            RunSession same = Resume(Setup(), QuietDrama(), saved, saved.MatchSeed);
            RunSession elsewhere = Resume(Setup(), QuietDrama(), saved, saved.MatchSeed ^ 0x9E3779B97F4A7C15UL);

            Assert.That(TableSignature(same), Is.EqualTo(historyAtSave), "played results are history");
            Assert.That(TableSignature(elsewhere), Is.EqualTo(historyAtSave), "on any seed at all");
            Assert.That(elsewhere.PlayedRounds, Is.EqualTo(played.PlayedRounds));

            same.AdvanceToEndOfSeason();
            elsewhere.AdvanceToEndOfSeason();

            // Compared over the whole remaining season rather than one round: four fixtures could coincide
            // by luck, ten weeks of them cannot, so this asserts divergence without asserting a coin flip.
            Assert.That(TableSignature(elsewhere), Is.Not.EqualTo(TableSignature(same)),
                "a different continuation seed must produce a different future");
        }

        // The table as a string — club, points, goal difference, in order. Enough to tell two seasons apart
        // and enough to prove two runs share a history.
        private static string TableSignature(RunSession session)
        {
            var text = new System.Text.StringBuilder();
            IReadOnlyList<LeagueTableRow> rows = session.Standings();
            for (int i = 0; i < rows.Count; i++)
            {
                text.Append(rows[i].Club.Value).Append(':').Append(rows[i].Points).Append('/')
                    .Append(rows[i].GoalDifference).Append('|');
            }

            return text.ToString();
        }

        // Fires exactly once a season, and the answer leaves a wound live for long enough that it is still
        // on the ledger several weeks later — which is what a round trip has to carry.
        private static RunBalance OneLastingDrama()
        {
            return new RunBalance(
                dramaEvents: new DramaCatalog(new[]
                {
                    new DramaEvent(
                        new DramaEventId("test-lasting-wound"), DramaCategory.Personal,
                        "drama.test.title", "drama.test.body",
                        requiresSubject: true,
                        new DramaTrigger(),
                        baseWeight: 1.0, cooldownWeeks: 0,
                        new[]
                        {
                            new DramaChoice("drama.test.wound", new[]
                            {
                                new DramaEffect(DramaEffectKind.SubjectMorale, -4.0, 30),
                            }),
                        }),
                }),
                drama: new DramaSettings(
                    maxEventsPerSeason: 1,
                    minWeeksBetweenEvents: 1,
                    weeklyChancePerWeight: 1.0,
                    maxWeeklyChance: 1.0));
        }

        /// <summary>
        /// The one that answers the owner's report end to end: he had to re-enter the market size, the cash
        /// and the wage budget on every load, his tactics and his chosen eleven were gone, and the drama
        /// budget came back full. So this plays a run that has all of those things in it, writes it through
        /// the SHIPPED codec (binary container, then the migrator, exactly as the file store does), resumes
        /// it against a setup whose every field is deliberately wrong — and asserts the run came back, not
        /// the window's input fields.
        /// </summary>
        [Test]
        public void SaveThenResume_CarriesTheWholeRun_NotJustTheSeason()
        {
            RunSession played = StartRun(Setup(), OneLastingDrama());

            // A signing, and a slice of the wage ceiling sold for cash — both are decisions, and both used
            // to be discarded by a reload that re-seeded the money from the setup.
            Player signing = MostAffordable(played);
            Assert.That(played.SignPlayer(signing).IsSuccess, Is.True);
            Assert.That(played.ShiftWageBudget(-5_000).IsSuccess, Is.True);

            // Tactics, a shape, and an eleven the auto-pick would never choose: the keeper and a striker
            // swapped, which no selector does by itself.
            var chosen = new Tactics(Mentality.Attacking, Tempo.Patient, Pressing.Contain, Approach.Counter);
            Assert.That(played.SetFormation(Formation.F433).IsSuccess, Is.True);
            Assert.That(played.SetTactics(chosen).IsSuccess, Is.True);
            LineupOutcome sheet = played.Lineup();
            Player keeper = sheet.Slots[0];
            Player striker = sheet.Slots[10];
            Assert.That(played.PlaceInSlot(0, striker.Id).IsSuccess, Is.True);

            // A week, a drama, an answer that leaves a live wound — then a few quiet weeks on top.
            played.AdvanceWeek();
            PendingDrama raised = played.PendingDrama;
            Assert.That(raised, Is.Not.Null, "the forced catalog fires in the first week");
            Player wounded = raised.Subject;
            Assert.That(played.ResolveDrama(0).IsSuccess, Is.True);
            for (int week = 0; week < 3; week++)
            {
                Assert.That(played.AdvanceWeek().IsSuccess, Is.True);
            }

            Assert.That(played.MoralePointsOf(wounded.Id), Is.EqualTo(-4.0).Within(1e-9), "the wound is still live at the save");

            Finances money = played.Finances;
            IReadOnlyList<Player> market = played.GetMarket();
            LineupOutcome before = played.Lineup();

            // Through the real bytes: the shipped codec writes the container, the migrator gates the load.
            SeasonSaveData reloaded = RoundTripThroughTheCodec(played.Capture());

            // Every knob here is wrong on purpose. If any of them shows up in the resumed run, the run is
            // still being re-seeded from a window.
            var wrongSetup = new RunSetup(
                teamCount: 20,
                seed: 1UL,
                managedClubIndex: 0,
                promotionPosition: 1,
                survivalPosition: 2,
                startingCash: 99_000_000L,
                weeklyWageBudget: 99_000L,
                marketSize: 40,
                guaranteedGems: 9,
                formation: Formation.F532,
                tactics: Tactics.Balanced);

            RunSession resumed = Resume(wrongSetup, OneLastingDrama(), reloaded, reloaded.MatchSeed);

            // The season, as before.
            Assert.That(resumed.PlayedRounds, Is.EqualTo(played.PlayedRounds));
            Assert.That(resumed.SeasonNumber, Is.EqualTo(played.SeasonNumber));
            Assert.That(resumed.ClubCount, Is.EqualTo(ClubCount));

            // The run's own setup, from the document rather than from wrongSetup.
            Assert.That(resumed.ManagedClub, Is.EqualTo(played.ManagedClub));
            Assert.That(resumed.ManagedClubName, Is.EqualTo(played.ManagedClubName));
            Assert.That(resumed.BoardTarget.PromotionPosition, Is.EqualTo(played.BoardTarget.PromotionPosition));
            Assert.That(resumed.BoardTarget.SurvivalPosition, Is.EqualTo(played.BoardTarget.SurvivalPosition));

            // The money — including the wage ceiling, which the manager moved himself.
            Assert.That(resumed.Finances.Cash, Is.EqualTo(money.Cash));
            Assert.That(resumed.Finances.WeeklyWageBudget, Is.EqualTo(money.WeeklyWageBudget));
            Assert.That(resumed.Finances.WeeklyWageBill, Is.EqualTo(money.WeeklyWageBill));

            // The signing is on the roster and off the market.
            Assert.That(resumed.Squad.Contains(signing.Id), Is.True, "the player he signed is still his");
            Assert.That(IdsOf(resumed.GetMarket()), Is.EqualTo(IdsOf(market)), "and the shortlist he was reading is the same one");

            // Tactics, shape and the exact team sheet.
            LineupOutcome after = resumed.Lineup();
            Assert.That(after.Formation.Name, Is.EqualTo("4-3-3"));
            Assert.That(after.Formation.Total, Is.EqualTo(before.Formation.Total));
            Assert.That(after.Tactics.Mentality, Is.EqualTo(Mentality.Attacking));
            Assert.That(after.Tactics.Tempo, Is.EqualTo(Tempo.Patient));
            Assert.That(after.Tactics.Pressing, Is.EqualTo(Pressing.Contain));
            Assert.That(after.Tactics.Approach, Is.EqualTo(Approach.Counter));
            Assert.That(after.Slots[0].Id, Is.EqualTo(striker.Id), "the eleven he picked, slot for slot");
            Assert.That(after.Slots[10].Id, Is.EqualTo(keeper.Id));
            Assert.That(IdsOf(after.Starters), Is.EqualTo(IdsOf(before.Starters)));

            // Morale is still on the ledger, with the weeks it had left.
            Assert.That(resumed.MoralePointsOf(wounded.Id), Is.EqualTo(-4.0).Within(1e-9));

            // And the drama engine remembers: this season's budget is spent, so the next week is quiet —
            // where a fresh engine would fire again immediately, which is what a reload used to hand back.
            Assert.That(resumed.AdvanceWeek().IsSuccess, Is.True);
            Assert.That(resumed.PendingDrama, Is.Null, "the season's one event was already spent");

            // The seed the world was generated from survives the round trip, on top of the seed the run is
            // now playing on.
            Assert.That(resumed.OriginalSeed, Is.EqualTo(Seed));
            Assert.That(resumed.Seed, Is.EqualTo(reloaded.MatchSeed));
        }

        [Test]
        public void Resume_FromAV5SaveWithNoRunBlock_FallsBackToTheCallersSetup()
        {
            // The migration path, from the run's side: a save written before schema v6 carries no money, no
            // tactics and no market, so those come from the setup exactly as they always did — the run
            // loads and plays rather than refusing a document that predates the field.
            RunSession played = StartRun(Setup(), QuietDrama());
            played.AdvanceWeek();

            SeasonSaveData saved = played.Capture();
            saved.SchemaVersion = 5;
            saved.Run = null;

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(saved);
            Assert.That(migrated.IsSuccess, Is.True, migrated.Error);

            var setup = new RunSetup(
                teamCount: ClubCount, seed: Seed, managedClubIndex: ManagedIndex,
                promotionPosition: 2, survivalPosition: 6,
                startingCash: 3_000_000L, weeklyWageBudget: 500_000L,
                marketSize: 7, guaranteedGems: 1);

            RunSession resumed = Resume(setup, QuietDrama(), migrated.Value, saved.MatchSeed);

            Assert.That(resumed.PlayedRounds, Is.EqualTo(1), "the season still resumes where it stopped");
            Assert.That(resumed.Finances.Cash, Is.EqualTo(3_000_000L), "the money comes from the setup");
            Assert.That(resumed.Finances.WeeklyWageBudget, Is.EqualTo(500_000L));
            Assert.That(resumed.GetMarket().Count, Is.EqualTo(7), "and a fresh market is generated at the setup's size");
            Assert.That(resumed.OriginalSeed, Is.EqualTo(saved.MatchSeed),
                "a v5 document's match seed IS the seed its world was generated from");
        }

        private static SeasonSaveData RoundTripThroughTheCodec(SeasonSaveData data)
        {
            var codec = new SaveSerializer();
            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(codec, SaveCodecFixtures.Write(codec, data));
            Assert.That(parsed.IsSuccess, Is.True, parsed.Error);

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(parsed.Value);
            Assert.That(migrated.IsSuccess, Is.True, migrated.Error);
            return migrated.Value;
        }

        private static List<int> IdsOf(IReadOnlyList<Player> players)
        {
            var ids = new List<int>(players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                ids.Add(players[i].Id.Value);
            }

            return ids;
        }
    }
}
