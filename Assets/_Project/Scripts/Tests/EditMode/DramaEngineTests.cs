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
                Finishing = stat, Technique = stat, FirstTouch = stat, Dribbling = stat, Passing = stat,
                Crossing = stat, Heading = stat, LongShots = stat, Marking = stat, Tackling = stat,
                Penalties = stat, FreeKicks = stat, Corners = stat, LongThrows = stat,
                Pace = stat, Acceleration = stat, Stamina = stat, Strength = stat, Agility = stat,
                Jumping = stat, Balance = stat, Positioning = stat,
                Reflexes = stat, Handling = stat, AerialReach = stat, CommandOfArea = stat,
                OneOnOnes = stat, Kicking = stat, GkPositioning = stat,
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

        private static DramaSettings AlwaysFire()
        {
            return new DramaSettings
            {
                MaxEventsPerSeason = 99,
                MinWeeksBetweenEvents = 1,
                WeeklyChancePerWeight = 1.0,
                MaxWeeklyChance = 1.0,
            };
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
            DramaSettings settings = AlwaysFire();
            settings.MaxEventsPerSeason = 4;
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
            DramaSettings settings = AlwaysFire();
            settings.MinWeeksBetweenEvents = 4;
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
            var settings = new DramaSettings
            {
                MaxEventsPerSeason = 99, MinWeeksBetweenEvents = 1, WeeklyChancePerWeight = 0.3, MaxWeeklyChance = 1.0,
            };

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

                Assert.That(inSeason, Is.LessThanOrEqualTo(DramaSettings.Default.MaxEventsPerSeason));
                totalEvents += inSeason;
            }

            double perSeason = totalEvents / (double)seasons;
            // Scarcity keeps drama valuable, silence kills it: a couple of events a season, never a feed.
            Assert.That(perSeason, Is.InRange(0.75, 4.0));
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
            var morale = new MoraleLedger();

            var pending = new PendingDrama(succession, captain, ContextOf(squad));
            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0, morale);

            Assert.That(outcome.IsSuccess, Is.True);
            // Youth outranks raw rating for the armband's future — the 20-year-old, not the better veteran.
            Assert.That(outcome.Value.TraitGrantTarget.Id, Is.EqualTo(heir.Id));
            Assert.That(outcome.Value.GrantedTrait, Is.EqualTo(new TraitId("dressing-room-leader")));
            Assert.That(morale.PointsOf(veteran.Id), Is.EqualTo(1.0).Within(1e-9), "the room lifts with the ceremony");
        }

        [Test]
        public void Resolve_ContractStandoff_TradesCashForAVeteransHeart()
        {
            DramaEvent standoff = DramaCatalog.Default.Find(new DramaEventId("contract-standoff"));
            Player veteran = PlayerOf(0, PlayerRole.Striker, 66, age: 31);
            var pending = new PendingDrama(standoff, veteran, ContextOf(new List<Player> { veteran }));
            var morale = new MoraleLedger();

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0, morale);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.CashDelta, Is.EqualTo(-500_000));
            Assert.That(morale.PointsOf(veteran.Id), Is.EqualTo(2.0).Within(1e-9));
        }

        [Test]
        public void Resolve_WageFineChoice_ReturnsTheFineAndWoundsTheSubject()
        {
            DramaEvent scandal = DramaCatalog.Default.Find(new DramaEventId("night-club-scandal"));
            Player subject = PlayerOf(3, PlayerRole.RightWing, 64, age: 23);
            var squad = new List<Player> { subject, PlayerOf(4, PlayerRole.Striker, 60) };
            var pending = new PendingDrama(scandal, subject, ContextOf(squad));
            var morale = new MoraleLedger();

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0, morale);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.CashDelta, Is.EqualTo(PlayerWage.Weekly(subject)));
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
            var morale = new MoraleLedger();

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 1, morale);

            Assert.That(outcome.IsSuccess, Is.True);
            foreach (Player player in squad)
            {
                Assert.That(morale.PointsOf(player.Id), Is.EqualTo(-2.0).Within(1e-9));
            }
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

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 1, new MoraleLedger());

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

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 0, new MoraleLedger(), currentCash: 10_000_000);

            Assert.That(outcome.IsSuccess, Is.True);
            Assert.That(outcome.Value.CashDelta, Is.EqualTo(-2_000_000));
        }

        [Test]
        public void Resolve_ChoiceOutOfRange_FailsWithResult()
        {
            DramaEvent cut = DramaCatalog.Default.Find(new DramaEventId("budget-cut"));
            var pending = new PendingDrama(cut, null, ContextOf(new List<Player>()));

            Result<DramaOutcome> outcome = new DramaEngine().Resolve(pending, 5, new MoraleLedger());

            Assert.That(outcome.IsSuccess, Is.False);
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
            LeagueSeason untouched = new LeagueSeason(TwoClubLeague());
            LeagueSeason wounded = new LeagueSeason(TwoClubLeague());
            MatchSimulator simulator = CreateSimulator();
            var context = new MatchContext(MatchImportance.Normal, 10000, false, false);

            foreach (Player player in wounded.SquadOf(new ClubId(0)).Players)
            {
                wounded.Morale.Apply(player.Id, -8.0, 4);
            }

            WeekResult plainWeek = untouched.AdvanceWeek(simulator, context, 91UL);
            WeekResult woundedWeek = wounded.AdvanceWeek(simulator, context, 91UL);

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
