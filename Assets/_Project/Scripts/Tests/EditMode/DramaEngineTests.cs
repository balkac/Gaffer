using System.Collections.Generic;
using Gaffer.Application.Drama;
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
    /// Locks the phase-4 exit criterion for drama: events are rare (budget, gap, cooldown — the
    /// frequency regression TDD §11 asks for), decided (a choice index resolves them, invalid ones
    /// fail), and state-changing (morale reaches next week's strength, cash and forced sales come
    /// back for their owners). And deterministic — same state, same seed, same drama.
    /// </summary>
    public sealed class DramaEngineTests
    {
        private static Player PlayerOf(int id, PlayerRole role, byte stat, int age = 24, byte potential = 70, params string[] traits)
        {
            var attributes = new Attributes
            {
                Finishing = stat,
                Technique = stat,
                FirstTouch = stat,
                Dribbling = stat,
                Passing = stat,
                Crossing = stat,
                Heading = stat,
                LongShots = stat,
                Marking = stat,
                Tackling = stat,
                Penalties = stat,
                FreeKicks = stat,
                Corners = stat,
                LongThrows = stat,
                Pace = stat,
                Acceleration = stat,
                Stamina = stat,
                Strength = stat,
                Agility = stat,
                Jumping = stat,
                Balance = stat,
                Positioning = stat,
                Reflexes = stat,
                Handling = stat,
                AerialReach = stat,
                CommandOfArea = stat,
                OneOnOnes = stat,
                Kicking = stat,
                GkPositioning = stat,
            };

            var ids = new List<TraitId>(traits.Length);
            foreach (string trait in traits)
            {
                ids.Add(new TraitId(trait));
            }

            return new Player(new PlayerId(id), "Player " + id, "England", role, age, attributes, potential, ids);
        }

        private static DramaWeekContext ContextOf(IReadOnlyList<Player> squad, int lossStreak = 0, bool windowOpen = false, IReadOnlyList<Player> starters = null, int tablePosition = 10)
        {
            return new DramaWeekContext(squad, starters, tablePosition, lossStreak, windowOpen);
        }

        // Settings are immutable, so the envelope a test wants to vary is a parameter here rather than
        // an assignment on the returned object.
        private static DramaSettings AlwaysFire(int maxEventsPerSeason = 99, int minWeeksBetweenEvents = 1)
        {
            return new DramaSettings(
                maxEventsPerSeason: maxEventsPerSeason,
                minWeeksBetweenEvents: minWeeksBetweenEvents,
                weeklyChancePerWeight: 1.0,
                maxWeeklyChance: 1.0);
        }

        private static DramaEvent SoloEvent(string id, DramaTrigger trigger, bool requiresSubject = false, int cooldown = 0, bool oncePerRun = false, IReadOnlyList<DramaTraitBias> subjectBiases = null, IReadOnlyList<DramaTraitBias> squadBiases = null)
        {
            return new DramaEvent(
                new DramaEventId(id), DramaCategory.Personal, "drama.test.title", "drama.test.body",
                requiresSubject, trigger, 1.0, cooldown,
                new[]
                {
                    new DramaChoice("drama.test.yes", new[] { new DramaEffect(DramaEffectKind.TeamMorale, 1.0, 2) }),
                    new DramaChoice("drama.test.no", System.Array.Empty<DramaEffect>()),
                },
                subjectBiases, squadBiases, oncePerRun);
        }

        [Test]
        public void TickWeek_NoEligibleCandidates_StaysQuiet()
        {
            var catalog = new DramaCatalog(new[] { SoloEvent("needs-streak", new DramaTrigger { MinLossStreak = 3 }) });
            var engine = new DramaEngine(catalog, AlwaysFire());
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            for (int week = 0; week < 38; week++)
            {
                Assert.That(engine.TickWeek(ContextOf(squad, lossStreak: 0), new SplitMix64RandomNumberGenerator((ulong)week)), Is.Null);
            }
        }

        [Test]
        public void TickWeek_SameStateAndSeed_RaisesTheSameDrama()
        {
            var squad = new List<Player>
            {
                PlayerOf(0, PlayerRole.Striker, 70, age: 22),
                PlayerOf(1, PlayerRole.CentralMidfield, 68, age: 26),
                PlayerOf(2, PlayerRole.CentreBack, 55, age: 29),
            };

            var first = new List<string>();
            var second = new List<string>();
            foreach (List<string> log in new[] { first, second })
            {
                var engine = new DramaEngine(DramaCatalog.Default, DramaSettings.Default);
                for (int week = 0; week < 38; week++)
                {
                    PendingDrama pending = engine.TickWeek(
                        ContextOf(squad, lossStreak: week % 6, windowOpen: week < 4),
                        new SplitMix64RandomNumberGenerator(900UL + (ulong)week));
                    log.Add(pending == null ? "-" : pending.Event.Id.Value + ":" + (pending.Subject?.Id.Value ?? -1));
                }
            }

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void TickWeek_SeasonBudget_CapsEventsPerSeason()
        {
            DramaSettings settings = AlwaysFire(maxEventsPerSeason: 4);
            var catalog = new DramaCatalog(new[] { SoloEvent("always", new DramaTrigger()) });
            var engine = new DramaEngine(catalog, settings);
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            int fired = 0;
            for (int week = 0; week < 38; week++)
            {
                if (engine.TickWeek(ContextOf(squad), new SplitMix64RandomNumberGenerator((ulong)week)) != null)
                {
                    fired++;
                }
            }

            Assert.That(fired, Is.EqualTo(4));

            // A new season resets the budget — the engine speaks again.
            engine.StartSeason();
            Assert.That(engine.TickWeek(ContextOf(squad), new SplitMix64RandomNumberGenerator(999UL)), Is.Not.Null);
        }

        [Test]
        public void TickWeek_MinimumGap_KeepsEventsApart()
        {
            DramaSettings settings = AlwaysFire(minWeeksBetweenEvents: 4);
            var catalog = new DramaCatalog(new[] { SoloEvent("always", new DramaTrigger()) });
            var engine = new DramaEngine(catalog, settings);
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            var firedWeeks = new List<int>();
            for (int week = 0; week < 20; week++)
            {
                if (engine.TickWeek(ContextOf(squad), new SplitMix64RandomNumberGenerator((ulong)week)) != null)
                {
                    firedWeeks.Add(week);
                }
            }

            for (int i = 1; i < firedWeeks.Count; i++)
            {
                Assert.That(firedWeeks[i] - firedWeeks[i - 1], Is.GreaterThanOrEqualTo(4));
            }

            Assert.That(firedWeeks.Count, Is.GreaterThan(1), "the gap test needs at least two events to compare");
        }

        [Test]
        public void TickWeek_EventCooldown_HoldsLongerThanTheGlobalGap()
        {
            DramaSettings settings = AlwaysFire();
            var catalog = new DramaCatalog(new[] { SoloEvent("rare", new DramaTrigger(), cooldown: 10) });
            var engine = new DramaEngine(catalog, settings);
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            var firedWeeks = new List<int>();
            for (int week = 0; week < 30; week++)
            {
                if (engine.TickWeek(ContextOf(squad), new SplitMix64RandomNumberGenerator((ulong)week)) != null)
                {
                    firedWeeks.Add(week);
                }
            }

            for (int i = 1; i < firedWeeks.Count; i++)
            {
                Assert.That(firedWeeks[i] - firedWeeks[i - 1], Is.GreaterThanOrEqualTo(10));
            }

            Assert.That(firedWeeks.Count, Is.GreaterThan(1));
        }

        [Test]
        public void TickWeek_OncePerRunSetPiece_NeverRepeats()
        {
            DramaSettings settings = AlwaysFire();
            var catalog = new DramaCatalog(new[] { SoloEvent("takeover", new DramaTrigger(), oncePerRun: true) });
            var engine = new DramaEngine(catalog, settings);
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            int fired = 0;
            for (int week = 0; week < 100; week++)
            {
                if (engine.TickWeek(ContextOf(squad), new SplitMix64RandomNumberGenerator((ulong)week)) != null)
                {
                    fired++;
                }

                if (week == 50)
                {
                    engine.StartSeason();
                }
            }

            Assert.That(fired, Is.EqualTo(1));
        }

        [Test]
        public void TickWeek_LoyalStar_DrawsFarFewerTransferRequests()
        {
            var loyalStar = PlayerOf(0, PlayerRole.Striker, 70, age: 27, potential: 70, "loyal");
            var plainStar = PlayerOf(1, PlayerRole.Striker, 70, age: 27, potential: 70);
            var squad = new List<Player> { loyalStar, plainStar };

            var catalog = new DramaCatalog(new[]
            {
                SoloEvent("transfer-request", new DramaTrigger { MinSubjectRating = 66.0 }, requiresSubject: true,
                    subjectBiases: new[] { new DramaTraitBias(new TraitId("loyal"), 0.2) }),
            });

            int loyalPicked = 0;
            int plainPicked = 0;
            for (ulong seed = 0; seed < 400; seed++)
            {
                var engine = new DramaEngine(catalog, AlwaysFire());
                PendingDrama pending = engine.TickWeek(ContextOf(squad), new SplitMix64RandomNumberGenerator(seed));
                Assert.That(pending, Is.Not.Null);
                if (pending.Subject.Id == loyalStar.Id)
                {
                    loyalPicked++;
                }
                else
                {
                    plainPicked++;
                }
            }

            // With a x0.2 bias the loyal star should carry roughly a sixth of the requests, not half.
            Assert.That(loyalPicked, Is.LessThan(plainPicked / 2));
            Assert.That(loyalPicked, Is.GreaterThan(0), "bias shrinks candidacy, it must not erase it");
        }

        [Test]
        public void TickWeek_LeaderInTheRoom_HalvesRiftFrequency()
        {
            var catalog = new DramaCatalog(new[]
            {
                SoloEvent("dressing-room-rift", new DramaTrigger { MinLossStreak = 3 },
                    squadBiases: new[] { new DramaTraitBias(new TraitId("dressing-room-leader"), 0.5) }),
            });
            var settings = new DramaSettings(
                maxEventsPerSeason: 99,
                minWeeksBetweenEvents: 1,
                weeklyChancePerWeight: 0.3,
                maxWeeklyChance: 1.0);

            var plainSquad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };
            var ledSquad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60, age: 24, potential: 70, "dressing-room-leader") };

            int plainFired = CountFires(catalog, settings, plainSquad);
            int ledFired = CountFires(catalog, settings, ledSquad);

            // The same seeds, the same streaks — only the leader in the room. His calm must be
            // frequency-real: measurably fewer rifts, not zero.
            Assert.That(ledFired, Is.LessThan(plainFired * 2 / 3));
            Assert.That(ledFired, Is.GreaterThan(0));
        }

        private static int CountFires(DramaCatalog catalog, DramaSettings settings, List<Player> squad)
        {
            int fired = 0;
            for (ulong seed = 0; seed < 600; seed++)
            {
                var engine = new DramaEngine(catalog, settings);
                if (engine.TickWeek(ContextOf(squad, lossStreak: 4), new SplitMix64RandomNumberGenerator(seed)) != null)
                {
                    fired++;
                }
            }

            return fired;
        }

        [Test]
        public void TickWeek_DefaultCatalogOverManySeasons_StaysRareButAlive()
        {
            var squad = new List<Player>
            {
                PlayerOf(0, PlayerRole.Striker, 70, age: 24),
                PlayerOf(1, PlayerRole.CentralMidfield, 68, age: 27, potential: 70, "press-magnet"),
                PlayerOf(2, PlayerRole.CentreBack, 62, age: 30),
                PlayerOf(3, PlayerRole.RightWing, 48, age: 18, potential: 88),
            };
            var starters = new List<Player> { squad[0], squad[1], squad[2] };

            int totalEvents = 0;
            const int seasons = 20;
            var engine = new DramaEngine(DramaCatalog.Default, DramaSettings.Default);
            for (int season = 0; season < seasons; season++)
            {
                engine.StartSeason();
                int inSeason = 0;
                for (int week = 0; week < 38; week++)
                {
                    PendingDrama pending = engine.TickWeek(
                        ContextOf(squad, lossStreak: week % 7, windowOpen: week < 4 || (week >= 19 && week < 23), starters: starters),
                        new SplitMix64RandomNumberGenerator(((ulong)season << 16) + (ulong)week));
                    if (pending != null)
                    {
                        inSeason++;
                    }
                }

                totalEvents += inSeason;
            }

            double perSeason = totalEvents / (double)seasons;

            // Both assertions here used to be unfalsifiable, and replacing them turned up a balance
            // finding rather than a test bug.
            //
            // The old code asserted `inSeason <= DramaSettings.Default.MaxEventsPerSeason` — the very
            // number the engine reads, so it agreed with itself at any value — and then
            // `perSeason InRange(0.75, 4.0)`, whose upper bound is that same cap. No run of capped
            // seasons can average ABOVE its cap, so the upper bound was unreachable and only the lower
            // one could ever fire. The test could report "rare" no matter how relentless drama became.
            //
            // MEASURED, default catalog and default settings: 4.00 events per season, in all 20 of the
            // 20 seasons. The budget is saturated every single season, so the only thing making drama
            // scarce is the hard cap — the weekly probability (WeeklyChancePerWeight 0.10, capped at
            // MaxWeeklyChance 0.35, with a 4-week minimum gap) fires whenever it is allowed to. That is
            // the "feed" the design says to avoid, held back by a ceiling rather than by rarity, and it
            // is why the old band's dead upper bound mattered. Pinned exactly here so the number is
            // visible and any recalibration is a deliberate, reviewed change; the lower bound stays as
            // the "drama must not go silent" guard.
            Assert.That(perSeason, Is.GreaterThan(0.75),
                $"Drama went quiet: {perSeason:F2} events a season over {seasons} seasons.");
            Assert.That(perSeason, Is.EqualTo(4.0).Within(1e-9),
                $"Drama frequency moved to {perSeason:F2}/season (was a saturated 4.00 — every season at " +
                "the MaxEventsPerSeason cap). If this is the intended recalibration, update the number " +
                "and the note above it.");
        }

        [Test]
        public void Catalog_DefaultEventSet_StaysInTheDesignBand()
        {
            // GDD's MVP menu: ~8-12 consequential, decided events — enough variety to feel alive,
            // small enough to stay authored. And every one of them must be a real decision.
            Assert.That(DramaCatalog.Default.Events.Count, Is.InRange(8, 12));
            foreach (DramaEvent dramaEvent in DramaCatalog.Default.Events)
            {
                Assert.That(dramaEvent.Choices.Count, Is.GreaterThanOrEqualTo(2),
                    dramaEvent.Id.Value + " must force a decision, not a notification");
            }
        }

        [Test]
        public void TickWeek_TraitGatedEvent_OnlyFiresForItsCarrier()
        {
            var trigger = new DramaTrigger { RequiredSubjectTrait = new TraitId("press-magnet") };
            var catalog = new DramaCatalog(new[] { SoloEvent("press-war", trigger, requiresSubject: true) });

            var quietSquad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };
            var quietEngine = new DramaEngine(catalog, AlwaysFire());
            for (int week = 0; week < 20; week++)
            {
                Assert.That(quietEngine.TickWeek(ContextOf(quietSquad), new SplitMix64RandomNumberGenerator((ulong)week)), Is.Null);
            }

            var magnet = PlayerOf(1, PlayerRole.RightWing, 60, age: 24, potential: 70, "press-magnet");
            var loudSquad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60), magnet };
            PendingDrama pending = new DramaEngine(catalog, AlwaysFire())
                .TickWeek(ContextOf(loudSquad), new SplitMix64RandomNumberGenerator(1UL));

            Assert.That(pending, Is.Not.Null);
            Assert.That(pending.Subject.Id, Is.EqualTo(magnet.Id), "the story belongs to the trait's carrier");
        }

        [Test]
        public void Resolve_CaptainAnointsSuccessor_TheLeaderTraitPassesToTheHeir()
        {
            DramaEvent succession = DramaCatalog.Default.Find(new DramaEventId("captain-succession"));
            Player captain = PlayerOf(0, PlayerRole.CentreBack, 66, age: 34, potential: 70, "dressing-room-leader");
            Player heir = PlayerOf(1, PlayerRole.CentralMidfield, 58, age: 20, potential: 85);
            Player veteran = PlayerOf(2, PlayerRole.Striker, 70, age: 28);
            var squad = new List<Player> { captain, heir, veteran };

            var pending = new PendingDrama(succession, captain, ContextOf(squad));
            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0);

            Assert.That(outcome.IsSuccess, Is.True);
            // Youth outranks raw rating for the armband's future — the 20-year-old, not the better veteran.
            Assert.That(outcome.Value.TraitGrantTarget.Id, Is.EqualTo(heir.Id));
            Assert.That(outcome.Value.GrantedTrait, Is.EqualTo(new TraitId("dressing-room-leader")));
            Assert.That(LedgerOf(outcome.Value).PointsOf(veteran.Id), Is.EqualTo(1.0).Within(1e-9), "the room lifts with the ceremony");
        }

        [Test]
        public void Resolve_ContractStandoff_TradesCashForAVeteransHeart()
        {
            DramaEvent standoff = DramaCatalog.Default.Find(new DramaEventId("contract-standoff"));
            Player veteran = PlayerOf(0, PlayerRole.Striker, 66, age: 31);
            var pending = new PendingDrama(standoff, veteran, ContextOf(new List<Player> { veteran }));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.CashDelta, Is.EqualTo(-500_000));
            Assert.That(LedgerOf(outcome.Value).PointsOf(veteran.Id), Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        public void Resolve_WageFineChoice_ReturnsTheFineAndWoundsTheSubject()
        {
            DramaEvent scandal = DramaCatalog.Default.Find(new DramaEventId("night-club-scandal"));
            Player subject = PlayerOf(3, PlayerRole.RightWing, 64, age: 23);
            var squad = new List<Player> { subject, PlayerOf(4, PlayerRole.Striker, 60) };
            var pending = new PendingDrama(scandal, subject, ContextOf(squad));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.CashDelta, Is.EqualTo(PlayerWage.Weekly(subject)));
            MoraleLedger morale = LedgerOf(outcome.Value);
            Assert.That(morale.PointsOf(subject.Id), Is.EqualTo(-2.0).Within(1e-9));
            Assert.That(morale.RatingMultiplierOf(subject.Id), Is.LessThan(1.0));
            Assert.That(morale.RatingMultiplierOf(squad[1].Id), Is.EqualTo(1.0).Within(1e-9), "the fine is personal, not team-wide");
        }

        [Test]
        public void Resolve_TeamMoraleChoice_TouchesTheWholeRoom()
        {
            DramaEvent rift = DramaCatalog.Default.Find(new DramaEventId("dressing-room-rift"));
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60), PlayerOf(1, PlayerRole.CentreBack, 60) };
            var pending = new PendingDrama(rift, null, ContextOf(squad, lossStreak: 3));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 1);

            Assert.That(outcome.IsSuccess, Is.True);
            MoraleLedger morale = LedgerOf(outcome.Value);
            foreach (Player player in squad)
            {
                Assert.That(morale.PointsOf(player.Id), Is.EqualTo(-2.0).Within(1e-9));
            }
        }

        [Test]
        public void Resolve_MoraleEffects_AreDescribedInTheOutcomeAndAppliedNowhere()
        {
            // The half-committed transaction this closes: Resolve used to write morale into the caller's
            // ledger in place, so an outcome did not describe everything it changed and a sale that then
            // failed left the wound applied (ARCHITECTURE §8/§8a). Nothing may move until an owner applies it.
            DramaEvent rift = DramaCatalog.Default.Find(new DramaEventId("dressing-room-rift"));
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60), PlayerOf(1, PlayerRole.CentreBack, 60) };
            var pending = new PendingDrama(rift, null, ContextOf(squad, lossStreak: 3));
            var untouched = new MoraleLedger();

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 1);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.MoraleChanges.Count, Is.EqualTo(squad.Count), "one entry per player in the room");
            foreach (MoraleChange change in outcome.Value.MoraleChanges)
            {
                Assert.That(change.Points, Is.EqualTo(-2.0).Within(1e-9));
                Assert.That(change.Weeks, Is.GreaterThan(0));
                Assert.That(untouched.PointsOf(change.Player), Is.EqualTo(0.0).Within(1e-9),
                    "resolving describes the morale change; it does not apply it");
            }
        }

        // Replays an outcome's morale entries onto a fresh ledger, exactly as RunSession.ResolveDrama
        // does — the outcome is now the only source of a morale change, so the test builds the ledger
        // from it rather than handing one in for the engine to write behind its back.
        private static MoraleLedger LedgerOf(DramaOutcome outcome)
        {
            var morale = new MoraleLedger();
            foreach (MoraleChange change in outcome.MoraleChanges)
            {
                morale.Apply(change.Player, change.Points, change.Weeks);
            }

            return morale;
        }

        [Test]
        public void Resolve_SellChoice_HandsTheSubjectBackForTheTransferOwnerToExecute()
        {
            DramaEvent request = DramaCatalog.Default.Find(new DramaEventId("transfer-request"));
            Player star = PlayerOf(0, PlayerRole.Striker, 70);
            var squadList = new List<Player> { star };
            for (int i = 1; i < 14; i++)
            {
                squadList.Add(PlayerOf(i, PlayerRole.CentreBack, 60));
            }

            var pending = new PendingDrama(request, star, ContextOf(squadList, windowOpen: true));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 1);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.PlayerToSell, Is.SameAs(star));

            // The owner executes the sale it owns — cash in, roster smaller, exactly as any transfer.
            var finances = new Finances(1_000_000, 100_000, 10_000);
            Result<TransferResult> sale = TransferService.Sell(finances, new Squad(squadList), star);
            Assert.That(sale.IsSuccess, Is.True);
            Assert.That(sale.Value.Finances.Cash, Is.GreaterThan(finances.Cash));
            Assert.That(sale.Value.Squad.Contains(star.Id), Is.False);
        }

        [Test]
        public void Resolve_CashFractionChoice_ScalesToTheClubsMoney()
        {
            DramaEvent cut = DramaCatalog.Default.Find(new DramaEventId("budget-cut"));
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };
            var pending = new PendingDrama(cut, null, ContextOf(squad));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0, currentCash: 10_000_000);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.CashDelta, Is.EqualTo(-2_000_000));
        }

        [Test]
        public void Resolve_ChoiceOutOfRange_FailsWithResult()
        {
            DramaEvent cut = DramaCatalog.Default.Find(new DramaEventId("budget-cut"));
            var pending = new PendingDrama(cut, null, ContextOf(new List<Player>()));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 5);

            Assert.That(outcome.IsSuccess, Is.False);
        }

        [Test]
        public void Resolve_NegativeChoiceIndex_FailsWithResult()
        {
            // The other end of the range check. `>= Count` alone would let -1 through to an index-out-of
            // -range throw, which is not how an expected failure leaves the core (§4).
            DramaEvent cut = DramaCatalog.Default.Find(new DramaEventId("budget-cut"));
            var pending = new PendingDrama(cut, null, ContextOf(new List<Player>()));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, -1);

            Assert.That(outcome.IsSuccess, Is.False);
        }

        [Test]
        public void Resolve_NoPendingDrama_FailsWithResultRatherThanThrowing()
        {
            // Answering a drama that is not there is a UI double-click, not a broken invariant, so it
            // comes back as a Result the caller can ignore (§4). Without the guard this is a
            // NullReferenceException out of the core.
            Result<DramaOutcome> outcome = new DramaEngine().Resolve(null, 0);

            Assert.That(outcome.IsFailure, Is.True);
            Assert.That(outcome.Error, Is.Not.Null.And.Not.Empty);
            Assert.That(outcome.Value, Is.Null);
        }

        // --- Trigger fields. Each of these gates which events can fire; each is asserted in BOTH
        // --- directions, because a condition that only ever passes and a condition that is inverted
        // --- look identical from one side (CONVENTIONS §5).

        // True when the event fired at least once over a fixed run of weeks under the given context.
        private static bool Fires(DramaTrigger trigger, DramaWeekContext context, bool requiresSubject = false)
        {
            var catalog = new DramaCatalog(new[] { SoloEvent("gated", trigger, requiresSubject: requiresSubject) });
            var engine = new DramaEngine(catalog, AlwaysFire());
            for (int week = 0; week < 20; week++)
            {
                if (engine.TickWeek(context, new SplitMix64RandomNumberGenerator((ulong)week)) != null)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TickWeek_MinTablePosition_FiresOnlyWhenTheClubIsThatFarDown()
        {
            // "At or below this 1-based position" — 18th is in trouble, 3rd is not.
            var trigger = new DramaTrigger { MinTablePosition = 17 };
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            Assert.That(Fires(trigger, ContextOf(squad, tablePosition: 18)), Is.True, "18th is at or below 17th");
            Assert.That(Fires(trigger, ContextOf(squad, tablePosition: 17)), Is.True, "the boundary position itself qualifies");
            Assert.That(Fires(trigger, ContextOf(squad, tablePosition: 16)), Is.False, "16th is above the pressure line");
            Assert.That(Fires(trigger, ContextOf(squad, tablePosition: 3)), Is.False);
        }

        [Test]
        public void TickWeek_RequiresOpenWindow_FiresOnlyWhileTheWindowIsOpen()
        {
            var trigger = new DramaTrigger { RequiresOpenWindow = true };
            var squad = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60) };

            Assert.That(Fires(trigger, ContextOf(squad, windowOpen: true)), Is.True);
            Assert.That(Fires(trigger, ContextOf(squad, windowOpen: false)), Is.False,
                "A transfer-flavoured event out of season would offer a move that cannot happen.");
        }

        [Test]
        public void TickWeek_MaxSubjectAge_PicksOnlyPlayersUpToThatAge()
        {
            var trigger = new DramaTrigger { MaxSubjectAge = 21 };
            var young = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60, age: 21) };
            var old = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60, age: 22) };

            Assert.That(Fires(trigger, ContextOf(young), requiresSubject: true), Is.True, "21 is at most 21");
            Assert.That(Fires(trigger, ContextOf(old), requiresSubject: true), Is.False);
        }

        [Test]
        public void TickWeek_MinSubjectAge_PicksOnlyPlayersFromThatAgeUp()
        {
            var trigger = new DramaTrigger { MinSubjectAge = 30 };
            var veteran = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60, age: 30) };
            var youngster = new List<Player> { PlayerOf(0, PlayerRole.Striker, 60, age: 29) };

            Assert.That(Fires(trigger, ContextOf(veteran), requiresSubject: true), Is.True, "30 is at least 30");
            Assert.That(Fires(trigger, ContextOf(youngster), requiresSubject: true), Is.False);
        }

        [Test]
        public void TickWeek_MinSubjectPotentialGap_PicksTheWonderkidNotTheFinishedArticle()
        {
            // The gap is potential minus CURRENT rating, so the filter has to be stated against a real
            // rating: both players below are rated by the same stat line, and only their ceilings differ.
            var trigger = new DramaTrigger { MinSubjectPotentialGap = 20.0 };
            Player wonderkid = PlayerOf(0, PlayerRole.Striker, 50, age: 18, potential: 95);
            Player finished = PlayerOf(0, PlayerRole.Striker, 50, age: 18, potential: 52);

            Assert.That(Fires(trigger, ContextOf(new List<Player> { wonderkid }), requiresSubject: true), Is.True);
            Assert.That(Fires(trigger, ContextOf(new List<Player> { finished }), requiresSubject: true), Is.False,
                "A player already at his ceiling has no 'he could be so much more' story.");
        }

        [Test]
        public void TickWeek_SubjectBenched_PicksTheReserveAndNeverTheStarter()
        {
            // The double negative in the engine reads "reject if he IS a starter". Inverting the inner
            // IsStarter call flips which player gets the grievance while leaving the event firing at the
            // same rate — so both directions are asserted, plus the unknown-lineup case.
            var trigger = new DramaTrigger { SubjectBenched = true };
            Player starter = PlayerOf(0, PlayerRole.Striker, 60);
            Player reserve = PlayerOf(1, PlayerRole.CentreBack, 60);
            var squad = new List<Player> { starter, reserve };
            var eleven = new List<Player> { starter };

            var catalog = new DramaCatalog(new[] { SoloEvent("benched", trigger, requiresSubject: true) });
            var subjects = new List<int>();
            for (ulong seed = 0; seed < 40; seed++)
            {
                PendingDrama pending = new DramaEngine(catalog, AlwaysFire())
                    .TickWeek(ContextOf(squad, starters: eleven), new SplitMix64RandomNumberGenerator(seed));
                if (pending != null)
                {
                    subjects.Add(pending.Subject.Id.Value);
                }
            }

            Assert.That(subjects, Is.Not.Empty, "the bench grievance must be reachable at all");
            Assert.That(subjects, Has.All.EqualTo(reserve.Id.Value),
                "Only the man out of the eleven has a bench grievance.");

            // And with no lineup known, nobody can be shown to be benched, so it must stay quiet rather
            // than treat "unknown" as "benched".
            Assert.That(Fires(trigger, ContextOf(squad, starters: null), requiresSubject: true), Is.False);

            // Sanity: without the flag the same squad and seeds do reach the starter, so the assertion
            // above is a real restriction and not an artefact of how the subject is drawn.
            var ungatedSubjects = new List<int>();
            var ungated = new DramaCatalog(new[] { SoloEvent("anyone", new DramaTrigger(), requiresSubject: true) });
            for (ulong seed = 0; seed < 40; seed++)
            {
                PendingDrama pending = new DramaEngine(ungated, AlwaysFire())
                    .TickWeek(ContextOf(squad, starters: eleven), new SplitMix64RandomNumberGenerator(seed));
                if (pending != null)
                {
                    ungatedSubjects.Add(pending.Subject.Id.Value);
                }
            }

            Assert.That(ungatedSubjects, Has.Some.EqualTo(starter.Id.Value));
        }

        [Test]
        public void MoraleLedger_WoundExpiresOnSchedule()
        {
            var morale = new MoraleLedger();
            var player = new PlayerId(7);

            morale.Apply(player, -4.0, 2);
            Assert.That(morale.RatingMultiplierOf(player), Is.LessThan(1.0));

            morale.TickWeek();
            Assert.That(morale.RatingMultiplierOf(player), Is.LessThan(1.0), "still open in its second week");

            morale.TickWeek();
            Assert.That(morale.RatingMultiplierOf(player), Is.EqualTo(1.0).Within(1e-9), "healed on schedule");
        }

        [Test]
        public void MoraleLedger_StackedDrama_IsClamped()
        {
            var morale = new MoraleLedger();
            var player = new PlayerId(7);

            morale.Apply(player, -20.0, 4);
            morale.Apply(player, -20.0, 4);

            Assert.That(morale.RatingMultiplierOf(player), Is.EqualTo(1.0 - (0.012 * 8.0)).Within(1e-9));
        }

        [Test]
        public void MoraleLedger_StackedGoodNews_IsClampedToo()
        {
            // The clamp is two-sided and only the negative arm was covered. A run of good weeks must not
            // be able to buy an unbounded rating multiplier — believability breaks in both directions.
            var morale = new MoraleLedger();
            var player = new PlayerId(7);

            morale.Apply(player, 20.0, 4);
            morale.Apply(player, 20.0, 4);

            Assert.That(morale.PointsOf(player), Is.EqualTo(MoraleSettings.Default.MaxAbsPoints).Within(1e-9));
            Assert.That(morale.RatingMultiplierOf(player), Is.EqualTo(1.0 + (0.012 * 8.0)).Within(1e-9));
        }

        [Test]
        public void MoraleLedger_TwoWoundsOfDifferentLengths_ExpireIndependently()
        {
            // Entries are stacked and each carries its own clock. A tick that dropped the whole player
            // when any one entry ran out — or that kept them all until the longest did — passes the
            // single-entry expiry test above; only overlapping durations tell them apart.
            var morale = new MoraleLedger();
            var player = new PlayerId(7);

            morale.Apply(player, -3.0, 2);
            morale.Apply(player, -1.0, 4);
            Assert.That(morale.PointsOf(player), Is.EqualTo(-4.0).Within(1e-9));

            morale.TickWeek();
            Assert.That(morale.PointsOf(player), Is.EqualTo(-4.0).Within(1e-9), "both still open in week two");

            morale.TickWeek();
            Assert.That(morale.PointsOf(player), Is.EqualTo(-1.0).Within(1e-9),
                "the two-week wound has faded; the four-week one has not");

            morale.TickWeek();
            Assert.That(morale.PointsOf(player), Is.EqualTo(-1.0).Within(1e-9));

            morale.TickWeek();
            Assert.That(morale.PointsOf(player), Is.Zero, "and now the long one is gone too");
        }

        [Test]
        public void MoraleLedger_AfterAWoundFades_ReturnsExactlyToBaseline()
        {
            // "Fades on schedule" has to mean back to neutral, not merely smaller: a player who has been
            // through drama and out the other side must rate exactly like one who never did.
            var morale = new MoraleLedger();
            var scarred = new PlayerId(1);
            var untouched = new PlayerId(2);

            morale.Apply(scarred, -6.0, 3);
            for (int week = 0; week < 3; week++)
            {
                morale.TickWeek();
            }

            Assert.That(morale.PointsOf(scarred), Is.Zero);
            Assert.That(morale.PointsOf(scarred), Is.EqualTo(morale.PointsOf(untouched)));
            Assert.That(morale.RatingMultiplierOf(scarred), Is.EqualTo(1.0).Within(1e-12));
            Assert.That(morale.RatingMultiplierOf(scarred), Is.EqualTo(morale.RatingMultiplierOf(untouched)));

            // And the ledger lets go of him entirely rather than keeping a spent entry forever.
            morale.Apply(scarred, -1.0, 1);
            morale.TickWeek();
            Assert.That(morale.PointsOf(scarred), Is.Zero);
        }

        [Test]
        public void Morale_LowTeamMorale_WeakensTheBuiltStrength()
        {
            var builder = new EffectiveStrengthBuilder();
            var eleven = new List<Player>();
            for (int i = 0; i < 11; i++)
            {
                PlayerRole role = i == 0 ? PlayerRole.Goalkeeper : i <= 4 ? PlayerRole.CentreBack : i <= 8 ? PlayerRole.CentralMidfield : PlayerRole.Striker;
                eleven.Add(PlayerOf(i, role, 60));
            }

            var morale = new MoraleLedger();
            foreach (Player player in eleven)
            {
                morale.Apply(player.Id, -6.0, 4);
            }

            TeamStrength neutral = builder.Build(eleven, Tactics.Balanced, default, null);
            TeamStrength wounded = builder.Build(eleven, Tactics.Balanced, default, morale);

            Assert.That(wounded.Attack, Is.LessThan(neutral.Attack));
            Assert.That(wounded.Midfield, Is.LessThan(neutral.Midfield));
            Assert.That(wounded.Defence, Is.LessThan(neutral.Defence));
        }

        [Test]
        public void Morale_AppliedThroughTheSeason_ChangesNextWeeksResult()
        {
            // Two identical leagues, same seed — in one, the home dressing room takes a team-wide hit
            // before the round. The drama must be felt in the scoreline, end to end.
            var untouched = new LeagueSeason(TwoClubLeague(), null, null, null, CreateSimulator());
            var wounded = new LeagueSeason(TwoClubLeague(), null, null, null, CreateSimulator());
            var context = new MatchContext(MatchImportance.Normal, 10000, false, false);

            foreach (Player player in wounded.SquadOf(new ClubId(0)).Players)
            {
                wounded.Morale.Apply(player.Id, -8.0, 4);
            }

            WeekResult plainWeek = untouched.AdvanceWeek(context, 91UL);
            WeekResult woundedWeek = wounded.AdvanceWeek(context, 91UL);

            MatchResult plainMatch = plainWeek.Matches[0];
            MatchResult woundedMatch = woundedWeek.Matches[0];
            bool sameScoreline = plainMatch.HomeGoals == woundedMatch.HomeGoals
                && plainMatch.AwayGoals == woundedMatch.AwayGoals
                && plainMatch.HomeShots == woundedMatch.HomeShots
                && plainMatch.AwayShots == woundedMatch.AwayShots;

            Assert.That(sameScoreline, Is.False);
        }

        private static League TwoClubLeague()
        {
            var strengthBuilder = new EffectiveStrengthBuilder();
            var home = new List<Player>();
            var away = new List<Player>();
            for (int i = 0; i < 11; i++)
            {
                PlayerRole role = i == 0 ? PlayerRole.Goalkeeper : i <= 4 ? PlayerRole.CentreBack : i <= 8 ? PlayerRole.CentralMidfield : PlayerRole.Striker;
                home.Add(PlayerOf(i, role, 62));
                away.Add(PlayerOf(100 + i, role, 62));
            }

            var homeSquad = new Squad(home);
            var awaySquad = new Squad(away);
            return new League("Drama League", new List<Club>
            {
                new Club(new ClubId(0), "Wounded FC", homeSquad, strengthBuilder.Build(homeSquad)),
                new Club(new ClubId(1), "Plain FC", awaySquad, strengthBuilder.Build(awaySquad)),
            });
        }

        private static MatchSimulator CreateSimulator()
        {
            return new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
        }
    }
}
