using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;
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
            return new RunBalance { Drama = new DramaSettings { MaxEventsPerSeason = 0 } };
        }

        private static RunSetup Setup()
        {
            return new RunSetup
            {
                TeamCount = ClubCount,
                Seed = Seed,
                ManagedClubIndex = ManagedIndex,
                PromotionPosition = 2,
                SurvivalPosition = 6,
                StartingCash = 6_000_000L,
                WeeklyWageBudget = 400_000L,
                MarketSize = 12,
                GuaranteedGems = 2,
            };
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
            RunSession session = StartRun(Setup(), QuietDrama());

            var throughSession = new List<MatchResult>();
            for (int week = 0; week < weeks; week++)
            {
                Result<WeekOutcome> outcome = session.AdvanceWeek();
                Assert.That(outcome.IsSuccess, Is.True, outcome.Error);
                throughSession.AddRange(outcome.Value.Matches);
            }

            IReadOnlyList<MatchResult> direct = PlayedDirectly(weeks);

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
        private static IReadOnlyList<MatchResult> PlayedDirectly(int weeks)
        {
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator(TraitCatalog.Default)), TraitCatalog.Default);
            League league = generator.Generate(ClubCount, new SplitMix64RandomNumberGenerator(Seed ^ 0x5EEDD5EEDUL));

            var simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver(),
                new WeightedScorerSelector(ScorerWeights.Default));

            var season = new LeagueSeason(league, TraitCatalog.Default, TacticsSettings.Default, MoraleSettings.Default, simulator);
            var managed = new ClubId(ManagedIndex);
            season.SetFormation(managed, Formation.F442);
            season.SetTactics(managed, Tactics.Balanced);
            season.SetStarters(managed, new List<Player>(new LineupSelector().SelectBest(season.SquadOf(managed), Formation.F442)));

            MatchContext context = RunSetup.Default.MatchContext;
            for (int week = 0; week < weeks; week++)
            {
                season.AdvanceWeek(context, Seed);
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
            return new RunBalance
            {
                DramaEvents = new DramaCatalog(new[] { forced }),
                Drama = new DramaSettings
                {
                    MaxEventsPerSeason = 99,
                    MinWeeksBetweenEvents = 1,
                    WeeklyChancePerWeight = 1.0,
                    MaxWeeklyChance = 1.0,
                },
            };
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
            Assert.That(session.Market, Contains.Item(subject));
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
            Assert.That(session.Market, Has.No.Member(target));
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
            Assert.That(session.Market, Contains.Item(target));
        }

        private static Player MostAffordable(RunSession session)
        {
            Player cheapest = null;
            foreach (Player player in session.Market)
            {
                if (cheapest == null || session.FeeOf(player) < session.FeeOf(cheapest))
                {
                    cheapest = player;
                }
            }

            Assert.That(cheapest, Is.Not.Null, "the market is generated with the run");
            return cheapest;
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
        public void StartNextSeason_MidSeason_IsRefused()
        {
            RunSession session = StartRun(Setup(), QuietDrama());
            session.AdvanceWeek();

            Result<SeasonRollover> rolled = session.StartNextSeason();

            Assert.That(rolled.IsFailure, Is.True);
            Assert.That(session.SeasonNumber, Is.EqualTo(1));
        }

        // ----- Save / resume ----------------------------------------------------------------------------

        [Test]
        public void Resume_FromACapturedRun_ContinuesTheSameFixturesExactly()
        {
            RunSession played = StartRun(Setup(), QuietDrama());
            for (int week = 0; week < 4; week++)
            {
                played.AdvanceWeek();
            }

            Gaffer.Application.Serialization.SeasonSaveData saved = played.Capture();
            Result<RunSession> resumedRun = RunSessionFactory.Resume(Setup(), QuietDrama(), saved);
            Assert.That(resumedRun.IsSuccess, Is.True, resumedRun.Error);
            RunSession resumed = resumedRun.Value;

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
    }
}
