using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
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

namespace Gaffer.Application.Run
{
    /// <summary>
    /// The run: one manager, one club, a league season at a time. This is the flow controller the game
    /// never had — it owns the league, the season, the finances, the drama engine and its morale ledger,
    /// the market and the season number, and every state change goes through one of its commands, each
    /// returning an outcome the view replays (ARCHITECTURE §8, CLAUDE.md NON-NEGOTIABLE #4).
    ///
    /// <para><b>Why it exists.</b> It was written twice, inside two editor windows — each with its own
    /// copy of the object graph, the weekly loop, the auto-pick and the rollover — and the copies had
    /// already drifted: one paid wages and ticked drama every week, the other did neither, and one read
    /// the managed squad from the live season while the other read a stale league. That is §8a's failure
    /// shape exactly: several call paths that each perform the operation themselves, and no single place
    /// where the order is expressed. The order now lives here, in one method per operation, and every
    /// path goes through it.</para>
    ///
    /// <para>Pure and synchronous, like everything in Application: no framework, no I/O. Persistence is
    /// a document in (<see cref="RunSessionFactory.Resume"/>) and a document out
    /// (<see cref="Capture"/>); writing the file belongs to Infrastructure (ARCHITECTURE §5).</para>
    ///
    /// <para>Deterministic: the same <see cref="RunSetup.Seed"/> and the same commands reproduce the run,
    /// match for match (NON-NEGOTIABLE #2). Match streams, world generation, the market and drama each
    /// derive their own stream from the seed, so a change in one cannot perturb another. The run never
    /// invents a seed — it cannot read a clock or a GUID, and it does not want to; a resume plays on
    /// whatever seed its caller hands <see cref="RunSessionFactory.Resume"/>, which is what lets the game
    /// re-roll the future while a test replays it exactly.</para>
    /// </summary>
    public sealed class RunSession
    {
        // The market's id space sits clear of the league's, and each season's clear of the last, so a
        // player signed from an earlier season's market can never collide with a current one.
        private const int MarketIdBase = 1_000_000;
        private const int MarketIdSeasonStride = 100_000;

        private static readonly IReadOnlyList<Player> NoPlayers = Array.Empty<Player>();
        private static readonly IReadOnlyList<MatchResult> NoResults = Array.Empty<MatchResult>();

        private readonly RunSetup _setup;
        private readonly RunBalance _balance;
        private readonly MatchSimulator _simulator;
        private readonly SeasonTransition _transition;
        private readonly LineupSelector _lineupSelector;
        private readonly EffectiveStrengthBuilder _strengthBuilder;
        private readonly PlayerPoolGenerator _marketGenerator;
        private readonly Scout _scout;
        private readonly DramaEngine _drama;
        private readonly SeasonEvaluator _evaluator = new SeasonEvaluator();
        private readonly BoardTarget _target;
        private readonly MatchContext _context;
        private readonly ClubId _managedClub;
        private readonly ulong _originalSeed;

        private League _league;
        private LeagueSeason _season;
        private Finances _finances;
        private List<Player> _market;
        private Player[] _slots;
        private Formation _formation;
        private Tactics _tactics;
        private PendingDrama _pending;
        private SeasonVerdict? _verdict;
        private int _seasonNumber;

        /// <summary>
        /// Built through <see cref="RunSessionFactory"/> only — one wiring seam for the whole graph, so
        /// two callers cannot guess at it differently (ARCHITECTURE §6). <paramref name="playedRounds"/>
        /// and <paramref name="playedResults"/> resume a season part-way through; zero and null start one.
        ///
        /// <para><paramref name="restored"/> is the rest of the run a save carries (v6) — money, tactics,
        /// the eleven, the market, morale, drama — and null starts one. THIS IS THE ONE PLACE THAT DECIDES
        /// WHAT AN ABSENT PIECE MEANS: each fallback below is written once, so a save without a market and
        /// a fresh run take the same path to having one, and the save adapter never has to invent a value
        /// to hand over.</para>
        /// </summary>
        internal RunSession(RunSetup setup, RunBalance balance, League league, int seasonNumber, int playedRounds, IReadOnlyList<MatchResult> playedResults, RunState restored)
        {
            _setup = setup;
            _balance = balance;
            _league = league;
            _seasonNumber = seasonNumber < 1 ? 1 : seasonNumber;

            // The seed the world was generated from, carried forward across every resume. It is NOT what
            // the coming weeks are played on — see the note on RunSessionFactory.Resume — so nothing in the
            // sim reads it; it exists so a run stays reproducible from its save for a bug report.
            _originalSeed = restored != null ? restored.OriginalSeed : setup.Seed;

            _simulator = new MatchSimulator(
                new PoissonChanceGenerator(balance.Simulation),
                new QualityChanceResolver(),
                new WeightedScorerSelector(balance.Scorer));
            _transition = new SeasonTransition(balance.Development, balance.Renewal, balance.Traits);
            _lineupSelector = new LineupSelector();
            _strengthBuilder = new EffectiveStrengthBuilder(balance.Traits, balance.TacticsBalance);
            _marketGenerator = new PlayerPoolGenerator(new PlayerGenerator(balance.Traits));
            _scout = new Scout(balance.Scouting);
            _drama = new DramaEngine(balance.DramaEvents, balance.Drama, balance.Economy);

            _context = setup.MatchContext;
            _target = new BoardTarget(setup.PromotionPosition, setup.SurvivalPosition);
            _managedClub = new ClubId(Clamp(setup.ManagedClubIndex, 0, league.Clubs.Count - 1));
            _formation = restored?.Formation ?? setup.Formation;
            _tactics = restored?.Tactics ?? setup.Tactics;

            _season = NewSeason(league, playedRounds, playedResults);
            RestoreMorale(restored);
            _finances = restored?.Finances ?? new Finances(setup.StartingCash, setup.WeeklyWageBudget, TotalWages(ManagedSquad()));
            _market = restored?.Market != null ? new List<Player>(restored.Market) : GenerateMarket();

            // Auto-pick first even when a sheet is being restored: it is what binds the shape and tactics
            // onto the season and fills any slot the saved sheet cannot name, so a restored eleven is a
            // correction to a complete team sheet rather than the only thing standing between the run and
            // an empty one.
            AutoPickAndBind();
            RestoreEleven(restored);
            RestoreDrama(restored);
            CheckComplete();
        }

        // ----- Queries: the read model a view renders from ---------------------------------------------

        public int SeasonNumber => _seasonNumber;

        /// <summary>
        /// The seed the coming weeks are derived from — the run's own seed at Start, and the CONTINUATION
        /// seed the caller chose at Resume. A save writes it to <c>SeasonSaveData.MatchSeed</c>, so handing
        /// it back to <see cref="RunSessionFactory.Resume"/> replays the same future exactly.
        /// </summary>
        public ulong Seed => _setup.Seed;

        /// <summary>
        /// The seed this run's WORLD was generated from. It survives every resume unchanged, and nothing in
        /// the simulation reads it — it is the number a bug report needs to rebuild the run from nothing,
        /// which is worth keeping even though the game never replays a run that way.
        /// </summary>
        public ulong OriginalSeed => _originalSeed;

        /// <summary>Rounds played so far this season — the week the run is on.</summary>
        public int PlayedRounds => _season.CurrentRound;

        public int RoundCount => _season.RoundCount;

        public bool IsSeasonComplete => _season.IsComplete;

        public ClubId ManagedClub => _managedClub;

        public string ManagedClubName => _league.Clubs[_managedClub.Value].Name;

        public int ClubCount => _league.Clubs.Count;

        public string LeagueName => _league.Name;

        public BoardTarget BoardTarget => _target;

        public Finances Finances => _finances;

        public Formation Formation => _formation;

        public Tactics Tactics => _tactics;

        /// <summary>The managed club's live roster — signings and sales are reflected here at once.</summary>
        public Squad Squad => ManagedSquad();

        /// <summary>This season's free agents, best-effort fresh each season.</summary>
        public IReadOnlyList<Player> Market => _market;

        /// <summary>The drama waiting on an answer, or null. A raised event blocks the next week.</summary>
        public PendingDrama PendingDrama => _pending;

        /// <summary>The board's verdict once the season is complete; null while it is being played.</summary>
        public SeasonVerdict? Verdict => _verdict;

        public TransferWindowPhase WindowPhase => TransferWindow.At(_season.CurrentRound, _season.RoundCount);

        public bool IsWindowOpen => WindowPhase != TransferWindowPhase.Closed;

        /// <summary>The managed club's league position, 1-based; 0 when it is not in the table.</summary>
        public int TablePosition => PositionOf(_managedClub);

        /// <summary>Consecutive league defeats the managed club is on.</summary>
        public int LossStreak => LossStreakOf(_managedClub);

        /// <summary>The table as it stands, best first. A render-time query, not a change record.</summary>
        public IReadOnlyList<LeagueTableRow> Standings()
        {
            return _season.Table.Ordered();
        }

        /// <summary>Everything a view draws about the eleven, derived once here.</summary>
        public LineupOutcome Lineup()
        {
            return BuildLineupOutcome();
        }

        public string ClubName(ClubId club)
        {
            return club.Value >= 0 && club.Value < _league.Clubs.Count ? _league.Clubs[club.Value].Name : string.Empty;
        }

        /// <summary>
        /// A player's name, for a club. The managed club is read through the live season (it may hold a
        /// just-signed scorer) and every other club through the league — the branch the windows each
        /// carried a copy of. Empty when he is not on that roster.
        /// </summary>
        public string PlayerName(ClubId club, PlayerId player)
        {
            Squad squad = club == _managedClub ? ManagedSquad() : ClubSquad(club);
            if (squad == null)
            {
                return string.Empty;
            }

            IReadOnlyList<Player> players = squad.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == player)
                {
                    return players[i].Name;
                }
            }

            return string.Empty;
        }

        /// <summary>The roster of any club, or null for a squad-less (strength-only) one.</summary>
        public Squad SquadOf(ClubId club)
        {
            return club == _managedClub ? ManagedSquad() : ClubSquad(club);
        }

        /// <summary>What this player would cost or fetch, on the run's economy balance.</summary>
        public long FeeOf(Player player)
        {
            return TransferService.Fee(player, _balance.Economy);
        }

        public long ValueOf(Player player)
        {
            return PlayerValuation.Value(player, _balance.Economy);
        }

        public long WeeklyWageOf(Player player)
        {
            return PlayerWage.Weekly(player, _balance.Economy);
        }

        /// <summary>
        /// The board's rate between the two budgets: €1/wk of wage ceiling is worth this many € of
        /// transfer cash, both ways (<see cref="ShiftWageBudget"/>). A view quotes it; it does not do the
        /// arithmetic with it.
        /// </summary>
        public int WageBudgetExchangeWeeks => _balance.Economy.WageBudgetExchangeWeeks;

        /// <summary>
        /// What <see cref="ShiftWageBudget"/> would leave, or the very message it would refuse with —
        /// asked without moving anything. A read-model query like <see cref="FeeOf"/>: the rule stays in
        /// one place, so a window can say <em>before</em> the click exactly what the click will answer
        /// instead of mirroring the two comparisons and drifting from them.
        /// </summary>
        public Result<Finances> PreviewWageBudgetShift(long weeklyDelta)
        {
            return _finances.ShiftWageBudget(weeklyDelta, _balance.Economy);
        }

        /// <summary>
        /// Live morale points on a player — the drama layer made visible on the roster (a wound is
        /// negative, a lift positive, both fading on schedule). A read model over the ledger: the ledger
        /// itself stays owned by the run, because applying to it is <see cref="ResolveDrama"/>'s job.
        /// </summary>
        public double MoralePointsOf(PlayerId player)
        {
            return _season.Morale.PointsOf(player);
        }

        /// <summary>What the manager knows about a prospect at this scouting accuracy (the mask, TDD §5).</summary>
        public ScoutReport Observe(Player player, double accuracy)
        {
            return _scout.Observe(player, accuracy);
        }

        /// <summary>
        /// The run as a serializable document, with the managed club's live roster folded back into the
        /// league first — capturing before that sync was how a just-signed player could be missing from a
        /// save, and it is an ordering constraint, so it has one owner (ARCHITECTURE §8a).
        /// </summary>
        public SeasonSaveData Capture()
        {
            SyncLeague();
            return new SeasonSaveMapper().Capture(_league, _season, _setup.Seed, _seasonNumber, CaptureRun());
        }

        // Everything the season document does not hold: the money, the manager's own decisions, the market
        // he is looking at, the morale his answers left behind, and the drama engine's memory. Domain types
        // out — turning them into a document is the mapper's job, not the run's (ARCHITECTURE §5).
        private RunState CaptureRun()
        {
            return new RunState(
                originalSeed: _originalSeed,
                setup: new RunSetupState(
                    // The index the run SETTLED on, not the raw setup number: it was clamped into the
                    // league at construction, and saving the unclamped one would resume a different club.
                    managedClubIndex: _managedClub.Value,
                    teamCount: _league.Clubs.Count,
                    promotionPosition: _setup.PromotionPosition,
                    survivalPosition: _setup.SurvivalPosition,
                    marketSize: _setup.MarketSize,
                    guaranteedGems: _setup.GuaranteedGems),
                finances: _finances,
                formation: _formation,
                tactics: _tactics,
                eleven: SlotIds(),
                market: _market,
                morale: _season.Morale.CaptureEntries(),
                drama: _drama.CaptureState(),
                pendingEvent: _pending != null ? _pending.Event.Id : default,
                pendingSubjectPlayerId: _pending?.Subject != null ? _pending.Subject.Id.Value : RunSaveData.NoPlayer);
        }

        private int[] SlotIds()
        {
            var ids = new int[_slots.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                ids[i] = _slots[i] != null ? _slots[i].Id.Value : RunSaveData.NoPlayer;
            }

            return ids;
        }

        // ----- Commands --------------------------------------------------------------------------------

        /// <summary>
        /// Plays one match week and returns everything it changed. The order is the whole point and it is
        /// expressed once, here: play the round (which ages morale a week), pay the wage bill out of
        /// transfer cash, tick drama on this week's state, then judge the season if that was the last
        /// round. Fails while a drama is pending — a decision is not a notification.
        /// </summary>
        public Result<WeekOutcome> AdvanceWeek()
        {
            if (_pending != null)
            {
                return Result<WeekOutcome>.Failure("There is a drama waiting on your answer — resolve it before playing on.");
            }

            if (_season.IsComplete)
            {
                return Result<WeekOutcome>.Failure("The season is over — start the next one.");
            }

            WeekResult week = _season.AdvanceWeek(_context, _setup.Seed);

            // Wages bite every week (GDD §4.4): the bill leaves the transfer cash whether or not the
            // week went well. The SeasonPlayer window skipped this, so its economy silently stood still.
            long wagesPaid = _finances.WeeklyWageBill;
            _finances = _finances.PayWeeklyWages();

            TickDrama();
            CheckComplete();

            return Result<WeekOutcome>.Success(BuildWeekOutcome(week, wagesPaid));
        }

        /// <summary>
        /// Plays out the rest of the season, stopping early where the story does: a raised drama ends the
        /// fast-forward, because it demands an answer. Returns one outcome per week played, in order.
        /// </summary>
        public Result<IReadOnlyList<WeekOutcome>> AdvanceToEndOfSeason()
        {
            if (_pending != null)
            {
                return Result<IReadOnlyList<WeekOutcome>>.Failure("There is a drama waiting on your answer — resolve it before playing on.");
            }

            if (_season.IsComplete)
            {
                return Result<IReadOnlyList<WeekOutcome>>.Failure("The season is over — start the next one.");
            }

            var weeks = new List<WeekOutcome>(_season.RoundCount - _season.CurrentRound);
            while (!_season.IsComplete)
            {
                Result<WeekOutcome> week = AdvanceWeek();
                if (week.IsFailure)
                {
                    break;
                }

                weeks.Add(week.Value);
                if (_pending != null)
                {
                    break;
                }
            }

            return Result<IReadOnlyList<WeekOutcome>>.Success(weeks);
        }

        /// <summary>
        /// Answers the pending drama — the single owner of that ordering (ARCHITECTURE §8a). The choice
        /// is read into an outcome that applies nothing, the forced sale (if any) is <em>tried against
        /// the money the cash effect leaves</em>, and only once every step is known to succeed does
        /// anything land: morale onto the ledger, cash into the finances, the sale through the transfer
        /// service, the sold player back onto the market, the granted trait onto a rebuilt teammate, the
        /// league re-synced and the eleven re-picked.
        ///
        /// <para><b>A sale that cannot go through rejects the whole resolution</b>, with nothing applied
        /// and the event still pending, rather than recording a partial result. Two reasons. First,
        /// replay honesty: this is the boundary a UI replays, so an outcome must describe a state the
        /// core is actually in — a record saying "he was fined and the room turned, but he did not
        /// leave" for a choice whose point was that he leaves is a lie the player can see. Second, it is
        /// reachable, not theoretical: <see cref="TransferService.Sell"/> refuses at the minimum squad
        /// size, and the old code applied the morale in the engine before the sale was even attempted,
        /// so that path left a half-committed transaction. The failure is expected and recoverable
        /// (CONVENTIONS §4), and no event in the catalog forces a sale on every choice — the manager can
        /// answer differently, which is a better story beat than a silent no-op.</para>
        /// </summary>
        public Result<DramaResolution> ResolveDrama(int choiceIndex)
        {
            if (_pending == null)
            {
                return Result<DramaResolution>.Failure("There is no pending drama to resolve.");
            }

            Result<DramaOutcome> resolved = _drama.Resolve(_pending, choiceIndex, _finances.Cash);
            if (resolved.IsFailure)
            {
                return Result<DramaResolution>.Failure(resolved.Error);
            }

            DramaOutcome outcome = resolved.Value;

            // --- Decide everything before changing anything -------------------------------------------
            Finances finances = outcome.CashDelta == 0
                ? _finances
                : new Finances(_finances.Cash + outcome.CashDelta, _finances.WeeklyWageBudget, _finances.WeeklyWageBill);

            TransferResult sale = null;
            if (outcome.PlayerToSell != null)
            {
                Squad squad = ManagedSquad();
                Result<TransferResult> attempt = squad == null
                    ? Result<TransferResult>.Failure("This club has no roster to sell from.")
                    : TransferService.Sell(finances, squad, outcome.PlayerToSell, _balance.Economy);
                if (attempt.IsFailure)
                {
                    return Result<DramaResolution>.Failure(
                        "That answer forces " + outcome.PlayerToSell.Name + " out, and the sale cannot go through: " +
                        attempt.Error + " Nothing has been applied — answer differently.");
                }

                sale = attempt.Value;
                finances = sale.Finances;
            }

            // --- Commit, in order ---------------------------------------------------------------------
            IReadOnlyList<MoraleChange> moraleChanges = outcome.MoraleChanges;
            for (int i = 0; i < moraleChanges.Count; i++)
            {
                _season.Morale.Apply(moraleChanges[i].Player, moraleChanges[i].Points, moraleChanges[i].Weeks);
            }

            _finances = finances;

            bool squadChanged = false;
            if (sale != null)
            {
                _season.UpdateSquad(_managedClub, sale.Squad);
                _market.Add(outcome.PlayerToSell);
                squadChanged = true;
            }

            Player rebuilt = null;
            if (outcome.TraitGrantTarget != null)
            {
                // The player is immutable — the heir is rebuilt with his new trait and swapped into the
                // live squad, so the aura is real from the next lineup on.
                rebuilt = WithTrait(outcome.TraitGrantTarget, outcome.GrantedTrait);
                Squad squad = ManagedSquad();
                if (squad != null)
                {
                    _season.UpdateSquad(_managedClub, squad.Remove(rebuilt.Id).Add(rebuilt));
                    squadChanged = true;
                }
            }

            if (squadChanged)
            {
                SyncLeague();
                AutoPickAndBind();
            }

            _pending = null;

            return Result<DramaResolution>.Success(new DramaResolution(
                eventId: outcome.EventId,
                choiceIndex: outcome.ChoiceIndex,
                cashDelta: outcome.CashDelta,
                finances: _finances,
                moraleChanges: moraleChanges,
                soldPlayer: sale != null ? outcome.PlayerToSell : null,
                saleFee: sale != null ? sale.Fee : 0L,
                traitGrantTarget: outcome.TraitGrantTarget,
                grantedTrait: outcome.GrantedTrait,
                rebuiltPlayer: rebuilt,
                lineup: BuildLineupOutcome()));
        }

        /// <summary>
        /// Rolls the whole league on a year and starts the next season: the managed club's live roster is
        /// folded back in first (so signings age and develop too), every squad ages, develops, retires
        /// its veterans and takes youth through, the wage bill is re-derived from the developed squad
        /// while the cash and the wage ceiling carry over, a new market opens, the drama budget resets and
        /// the eleven is re-picked. Fails while the season is still being played.
        /// </summary>
        public Result<SeasonRollover> StartNextSeason()
        {
            if (!_season.IsComplete)
            {
                return Result<SeasonRollover>.Failure("The season is not finished — play it out first.");
            }

            SyncLeague();
            Squad before = ManagedSquad();
            IReadOnlyList<Player> beforePlayers = before != null ? before.Players : NoPlayers;

            _seasonNumber++;
            _league = _transition.ToNextSeason(_league, _setup.Seed, _seasonNumber);
            _season = NewSeason(_league, 0, null);

            Squad after = ManagedSquad();
            IReadOnlyList<Player> afterPlayers = after != null ? after.Players : NoPlayers;

            // The ceiling carries over as it stands rather than being re-seeded from the setup, because a
            // slice of it sold for cash through ShiftWageBudget stays sold. Re-seeding would hand that
            // slice back every summer while the cash it paid for stayed in the bank — sell 5k/wk each
            // season and the club prints the rate every rollover. With no shift performed the live ceiling
            // *is* the setup's, so nothing else changes.
            _finances = new Finances(_finances.Cash, _finances.WeeklyWageBudget, TotalWages(after));
            _market = GenerateMarket();
            _drama.StartSeason();
            _pending = null;
            _verdict = null;
            AutoPickAndBind();

            return Result<SeasonRollover>.Success(new SeasonRollover(
                seasonNumber: _seasonNumber,
                retired: Missing(beforePlayers, afterPlayers),
                arrived: Missing(afterPlayers, beforePlayers),
                finances: _finances,
                market: _market,
                lineup: BuildLineupOutcome()));
        }

        /// <summary>
        /// Signs a free agent: the fee and the wage are checked against the money, the live squad takes
        /// him the same week, the league is re-synced and the eleven re-picked — one step, one outcome.
        /// Only in an open window.
        /// </summary>
        public Result<TransferOutcome> SignPlayer(Player player)
        {
            if (player == null)
            {
                return Result<TransferOutcome>.Failure("There is no player to sign.");
            }

            if (!IsWindowOpen)
            {
                return Result<TransferOutcome>.Failure("The transfer window is closed.");
            }

            Squad squad = ManagedSquad();
            if (squad == null)
            {
                return Result<TransferOutcome>.Failure("This club has no roster to sign into.");
            }

            Result<TransferResult> result = TransferService.Sign(_finances, squad, player, _balance.Economy);
            if (result.IsFailure)
            {
                return Result<TransferOutcome>.Failure(result.Error);
            }

            _finances = result.Value.Finances;
            _season.UpdateSquad(_managedClub, result.Value.Squad);
            _market.Remove(player);
            SyncLeague();
            AutoPickAndBind();

            return Result<TransferOutcome>.Success(new TransferOutcome(
                player: player,
                isSale: false,
                fee: result.Value.Fee,
                weeklyWage: WeeklyWageOf(player),
                finances: _finances,
                lineup: BuildLineupOutcome()));
        }

        /// <summary>Sells a squad player back onto the market. Only in an open window.</summary>
        public Result<TransferOutcome> SellPlayer(Player player)
        {
            if (player == null)
            {
                return Result<TransferOutcome>.Failure("There is no player to sell.");
            }

            if (!IsWindowOpen)
            {
                return Result<TransferOutcome>.Failure("The transfer window is closed.");
            }

            Squad squad = ManagedSquad();
            if (squad == null)
            {
                return Result<TransferOutcome>.Failure("This club has no roster to sell from.");
            }

            Result<TransferResult> result = TransferService.Sell(_finances, squad, player, _balance.Economy);
            if (result.IsFailure)
            {
                return Result<TransferOutcome>.Failure(result.Error);
            }

            _finances = result.Value.Finances;
            _season.UpdateSquad(_managedClub, result.Value.Squad);
            _market.Add(player);
            SyncLeague();
            AutoPickAndBind();

            return Result<TransferOutcome>.Success(new TransferOutcome(
                player: player,
                isSale: true,
                fee: result.Value.Fee,
                weeklyWage: WeeklyWageOf(player),
                finances: _finances,
                lineup: BuildLineupOutcome()));
        }

        /// <summary>
        /// Moves money between the two budgets at the board's rate: a positive
        /// <paramref name="weeklyDelta"/> buys that much weekly wage ceiling with transfer cash, a
        /// negative one gives up that much ceiling for cash. <see cref="Finances.ShiftWageBudget(long, Transfers.EconomySettings)"/>
        /// owns the rule and the refusals; this is the command that commits the answer and hands back the
        /// new money for the view to replay.
        ///
        /// <para><b>Always available</b>, unlike <see cref="SignPlayer"/> and <see cref="SellPlayer"/>:
        /// this moves no player, so there is no window to be in. It is the answer to being stuck with
        /// cash you cannot spend and no room to spend it in, and being told to wait for the window would
        /// leave the manager stuck for exactly as long as the problem is worth solving.</para>
        /// </summary>
        public Result<BudgetShiftOutcome> ShiftWageBudget(long weeklyDelta)
        {
            Result<Finances> shifted = _finances.ShiftWageBudget(weeklyDelta, _balance.Economy);
            if (shifted.IsFailure)
            {
                return Result<BudgetShiftOutcome>.Failure(shifted.Error);
            }

            Finances before = _finances;
            _finances = shifted.Value;

            return Result<BudgetShiftOutcome>.Success(new BudgetShiftOutcome(
                weeklyWageBudgetDelta: _finances.WeeklyWageBudget - before.WeeklyWageBudget,
                cashDelta: _finances.Cash - before.Cash,
                finances: _finances));
        }

        /// <summary>Changes shape and re-picks the best eleven for it; applies from next week.</summary>
        public Result<LineupOutcome> SetFormation(Formation formation)
        {
            if (formation.Slots == null || formation.Total == 0)
            {
                return Result<LineupOutcome>.Failure("That formation has no slots.");
            }

            _formation = formation;
            AutoPickAndBind();
            return Result<LineupOutcome>.Success(BuildLineupOutcome());
        }

        /// <summary>Changes the tactical setup; applies to the managed club from next week.</summary>
        public Result<LineupOutcome> SetTactics(Tactics tactics)
        {
            _tactics = tactics;
            _season.SetTactics(_managedClub, _tactics);
            return Result<LineupOutcome>.Success(BuildLineupOutcome());
        }

        /// <summary>Re-picks the whole eleven from the squad, discarding manual changes.</summary>
        public Result<LineupOutcome> AutoPickLineup()
        {
            AutoPickAndBind();
            return Result<LineupOutcome>.Success(BuildLineupOutcome());
        }

        /// <summary>
        /// Puts a player in a slot: if he was already on the pitch the two swap, if he came off the bench
        /// he takes the slot and whoever was there is benched.
        /// </summary>
        public Result<LineupOutcome> PlaceInSlot(int slot, PlayerId player)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                return Result<LineupOutcome>.Failure($"Slot {slot} is not on this formation's team sheet.");
            }

            Player moving = FindInSquad(player);
            if (moving == null)
            {
                return Result<LineupOutcome>.Failure("That player is not in your squad.");
            }

            int from = SlotOf(player);
            if (from != slot)
            {
                Player occupant = _slots[slot];
                _slots[slot] = moving;
                if (from >= 0)
                {
                    _slots[from] = occupant;
                }

                BindStarters();
            }

            return Result<LineupOutcome>.Success(BuildLineupOutcome());
        }

        /// <summary>Empties a slot, benching whoever was in it.</summary>
        public Result<LineupOutcome> ClearSlot(int slot)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                return Result<LineupOutcome>.Failure($"Slot {slot} is not on this formation's team sheet.");
            }

            if (_slots[slot] != null)
            {
                _slots[slot] = null;
                BindStarters();
            }

            return Result<LineupOutcome>.Success(BuildLineupOutcome());
        }

        /// <summary>Benches a starter, or gives a benched player the first free slot.</summary>
        public Result<LineupOutcome> ToggleStarter(PlayerId player)
        {
            int slot = SlotOf(player);
            if (slot >= 0)
            {
                _slots[slot] = null;
                BindStarters();
                return Result<LineupOutcome>.Success(BuildLineupOutcome());
            }

            Player joining = FindInSquad(player);
            if (joining == null)
            {
                return Result<LineupOutcome>.Failure("That player is not in your squad.");
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    _slots[i] = joining;
                    BindStarters();
                    return Result<LineupOutcome>.Success(BuildLineupOutcome());
                }
            }

            return Result<LineupOutcome>.Failure("The eleven is full — bench someone first.");
        }

        // ----- The season's collaborators ---------------------------------------------------------------

        // Every LeagueSeason in the run is built here, on the run's own catalogs, balance and simulator —
        // a fresh season and a resumed one are wired identically, which is what stops a reload from
        // playing on different rules than the run it continues.
        private LeagueSeason NewSeason(League league, int playedRounds, IReadOnlyList<MatchResult> playedResults)
        {
            return LeagueSeason.Restore(
                league,
                playedRounds,
                playedResults ?? NoResults,
                _balance.Traits,
                _balance.TacticsBalance,
                _balance.Morale,
                _simulator);
        }

        private Squad ManagedSquad()
        {
            return _season.SquadOf(_managedClub);
        }

        private Squad ClubSquad(ClubId club)
        {
            return club.Value >= 0 && club.Value < _league.Clubs.Count ? _league.Clubs[club.Value].Squad : null;
        }

        // Folds the managed club's live roster (after any signing, sale or drama) back into the league,
        // re-deriving its strength through the run's own trait catalog — the windows used a default-catalog
        // builder here, so a configured catalog produced a league whose strengths did not match its season.
        private void SyncLeague()
        {
            Squad live = ManagedSquad();
            if (live == null)
            {
                return;
            }

            var clubs = new List<Club>(_league.Clubs);
            Club old = clubs[_managedClub.Value];
            clubs[_managedClub.Value] = new Club(old.Id, old.Name, live, _strengthBuilder.Build(live));
            _league = new League(_league.Name, clubs);
        }

        private long TotalWages(Squad squad)
        {
            if (squad == null)
            {
                return 0L;
            }

            long total = 0L;
            IReadOnlyList<Player> players = squad.Players;
            for (int i = 0; i < players.Count; i++)
            {
                total += PlayerWage.Weekly(players[i], _balance.Economy);
            }

            return total;
        }

        // A free-agent market to scout and sign from, with a few guaranteed gems (TDD §5). Both the
        // generation seed and the id range shift with the season number, so each season shows a genuinely
        // fresh set of prospects rather than the same names again.
        private List<Player> GenerateMarket()
        {
            var gem = new GenerationContext
            {
                MinAge = 16,
                MaxAge = 19,
                MinAbility = 35,
                MaxAbility = 52,
                MinPotential = 84,
                MaxPotential = 95,
            };

            IReadOnlyList<Player> pool = _marketGenerator.GeneratePool(
                _setup.MarketSize < 1 ? 1 : _setup.MarketSize,
                _setup.GuaranteedGems < 0 ? 0 : _setup.GuaranteedGems,
                new GenerationContext(),
                gem,
                new SplitMix64RandomNumberGenerator((_setup.Seed ^ 0xA5A5A5UL) + ((ulong)_seasonNumber * 0x9E3779B97F4A7C15UL)));

            int idBase = MarketIdBase + (_seasonNumber * MarketIdSeasonStride);
            var market = new List<Player>(pool.Count);
            for (int i = 0; i < pool.Count; i++)
            {
                Player p = pool[i];
                market.Add(new Player(new PlayerId(idBase + p.Id.Value), p.Name, p.Nationality, p.Role, p.Age, p.Attributes, p.HiddenPotential, p.Traits));
            }

            return market;
        }

        private static Player WithTrait(Player player, TraitId trait)
        {
            var traits = new List<TraitId>(player.Traits.Count + 1);
            for (int i = 0; i < player.Traits.Count; i++)
            {
                traits.Add(player.Traits[i]);
            }

            traits.Add(trait);
            return new Player(player.Id, player.Name, player.Nationality, player.Role, player.Age, player.Attributes, player.HiddenPotential, traits);
        }

        // ----- Resuming a saved run ----------------------------------------------------------------------

        // Each of these is a no-op when the save did not carry that piece, which is what makes an absent
        // group in the document mean "leave it as a fresh run would have it" (see the constructor).

        private void RestoreMorale(RunState restored)
        {
            IReadOnlyList<MoraleEntry> morale = restored?.Morale;
            if (morale == null)
            {
                return;
            }

            // Applied rather than injected: an entry's REMAINING weeks is a duration from where the run
            // now stands, so the ordinary Apply is the exact inverse of the capture and there is no second
            // way into the ledger to keep in step with it.
            for (int i = 0; i < morale.Count; i++)
            {
                MoraleEntry entry = morale[i];
                _season.Morale.Apply(entry.Player, entry.Points, entry.WeeksLeft);
            }
        }

        // A saved sheet names players by id, so a squad that has changed underneath the save degrades one
        // slot at a time: an id nobody carries leaves that slot empty rather than refusing the run.
        private void RestoreEleven(RunState restored)
        {
            IReadOnlyList<int> eleven = restored?.Eleven;
            if (eleven == null)
            {
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = i < eleven.Count && eleven[i] != RunSaveData.NoPlayer
                    ? FindInSquad(new PlayerId(eleven[i]))
                    : null;
            }

            BindStarters();
        }

        private void RestoreDrama(RunState restored)
        {
            if (restored == null)
            {
                return;
            }

            _drama.RestoreState(restored.Drama);
            _pending = RestorePending(restored);
        }

        // The unanswered event, rebuilt against the live catalog and the live squad. Only the event's id
        // and its subject are saved: everything else a PendingDrama holds is this week's context, and every
        // part of that — the room, the eleven, the position, the streak, the window — is already restored
        // above, so storing it would be storing a derivation.
        //
        // Tolerant (ARCHITECTURE §11): an event the catalog no longer defines, or a subject who is no
        // longer at the club, clears the block instead of failing the load. The manager loses a decision he
        // never got to make; the alternative is a run that cannot be opened.
        private PendingDrama RestorePending(RunState restored)
        {
            if (string.IsNullOrEmpty(restored.PendingEvent.Value))
            {
                return null;
            }

            DramaEvent raised = _balance.DramaEvents.Find(restored.PendingEvent);
            if (raised == null)
            {
                return null;
            }

            Squad squad = ManagedSquad();
            if (squad == null)
            {
                return null;
            }

            Player subject = restored.PendingSubjectPlayerId == RunSaveData.NoPlayer
                ? null
                : FindInSquad(new PlayerId(restored.PendingSubjectPlayerId));
            if (raised.RequiresSubject && subject == null)
            {
                return null;
            }

            return new PendingDrama(raised, subject, DramaContext(squad));
        }

        // ----- Lineup ownership ------------------------------------------------------------------------

        // Re-picks the best eleven for the current formation and binds shape, sheet and tactics onto the
        // season. Every path that changes the roster ends here, so "the squad changed, so the eleven and
        // the club's strength must be re-derived" is expressed once instead of at each call site.
        private void AutoPickAndBind()
        {
            _slots = new Player[_formation.Total];
            Squad squad = ManagedSquad();
            if (squad != null)
            {
                // Copied out of the selector's reusable return buffer, whose documented lifetime ends at
                // the next SelectBest call.
                IReadOnlyList<Player> eleven = _lineupSelector.SelectBest(squad, _formation);
                for (int i = 0; i < eleven.Count && i < _slots.Length; i++)
                {
                    _slots[i] = eleven[i];
                }
            }

            _season.SetFormation(_managedClub, _formation);
            _season.SetTactics(_managedClub, _tactics);
            BindStarters();
        }

        private void BindStarters()
        {
            _season.SetStarters(_managedClub, StartersList());
        }

        private List<Player> StartersList()
        {
            var starters = new List<Player>(_slots.Length);
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null)
                {
                    starters.Add(_slots[i]);
                }
            }

            return starters;
        }

        private int SlotOf(PlayerId player)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] != null && _slots[i].Id == player)
                {
                    return i;
                }
            }

            return -1;
        }

        private Player FindInSquad(PlayerId id)
        {
            Squad squad = ManagedSquad();
            if (squad == null)
            {
                return null;
            }

            IReadOnlyList<Player> players = squad.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id == id)
                {
                    return players[i];
                }
            }

            return null;
        }

        private LineupOutcome BuildLineupOutcome()
        {
            var slots = new Player[_slots.Length];
            Array.Copy(_slots, slots, _slots.Length);
            List<Player> starters = StartersList();

            var bench = new List<Player>();
            Squad squad = ManagedSquad();
            if (squad != null)
            {
                IReadOnlyList<Player> players = squad.Players;
                for (int i = 0; i < players.Count; i++)
                {
                    if (SlotOf(players[i].Id) < 0)
                    {
                        bench.Add(players[i]);
                    }
                }
            }

            return new LineupOutcome(
                formation: _formation,
                tactics: _tactics,
                slots: slots,
                starters: starters,
                bench: bench,
                isComplete: starters.Count == _formation.Total,
                strength: starters.Count > 0
                    ? _strengthBuilder.Build(starters, _tactics)
                    : _league.Clubs[_managedClub.Value].Strength,
                chanceProfile: ChanceProfile.FromTactics(_tactics, _balance.TacticsBalance));
        }

        // ----- Drama ------------------------------------------------------------------------------------

        // The weekly drama tick (TDD §8) for the managed club: hand the engine this week's snapshot —
        // roster, eleven, table position, form, window — and let it decide, on its own seeded stream
        // (independent of the match streams), whether a story surfaces. A finished season stays quiet.
        private void TickDrama()
        {
            if (_season.IsComplete)
            {
                return;
            }

            Squad squad = ManagedSquad();
            if (squad == null)
            {
                return;
            }

            _pending = _drama.TickWeek(DramaContext(squad), new SplitMix64RandomNumberGenerator(DramaSeed()));
        }

        // This week's state as the drama layer sees it. Shared with the resume path, which rebuilds a saved
        // pending event's context rather than storing it — one definition of "what this week looks like",
        // so a restored decision cannot be answered against a different room than a fresh one.
        private DramaWeekContext DramaContext(Squad squad)
        {
            return new DramaWeekContext(
                squad.Players,
                StartersList(),
                TablePosition,
                LossStreak,
                IsWindowOpen);
        }

        // A well-distributed per-run, per-season, per-week seed, mixed apart from every match stream so
        // drama and results cannot perturb one another.
        private ulong DramaSeed()
        {
            unchecked
            {
                ulong z = _setup.Seed ^ 0xD7A3AD7A3AUL;
                z ^= (ulong)(uint)_seasonNumber * 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z ^= (ulong)(uint)_season.CurrentRound * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        // ----- Derivations the view used to do for itself ------------------------------------------------

        private void CheckComplete()
        {
            if (_season.IsComplete && _verdict == null)
            {
                _verdict = _evaluator.Evaluate(_season.Table, _managedClub, _target);
            }
        }

        private int PositionOf(ClubId club)
        {
            IReadOnlyList<LeagueTableRow> rows = _season.Table.Ordered();
            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].Club == club)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private int LossStreakOf(ClubId club)
        {
            int streak = 0;
            IReadOnlyList<MatchResult> results = _season.PlayedResults;
            for (int i = results.Count - 1; i >= 0; i--)
            {
                MatchResult match = results[i];
                bool home = match.Home == club;
                bool away = match.Away == club;
                if (!home && !away)
                {
                    continue;
                }

                bool lost = home ? match.HomeGoals < match.AwayGoals : match.AwayGoals < match.HomeGoals;
                if (!lost)
                {
                    break;
                }

                streak++;
            }

            return streak;
        }

        private WeekOutcome BuildWeekOutcome(WeekResult week, long wagesPaid)
        {
            MatchResult? managed = null;
            for (int i = 0; i < week.Matches.Count; i++)
            {
                MatchResult match = week.Matches[i];
                if (match.Home == _managedClub || match.Away == _managedClub)
                {
                    managed = match;
                    break;
                }
            }

            int position = TablePosition;
            return new WeekOutcome(
                round: week.Round,
                playedRounds: _season.CurrentRound,
                roundCount: _season.RoundCount,
                isSeasonComplete: _season.IsComplete,
                matches: week.Matches,
                managedMatch: managed,
                tablePosition: position,
                lossStreak: LossStreak,
                finances: _finances,
                wagesPaid: wagesPaid,
                windowPhase: WindowPhase,
                drama: _pending,
                verdict: _verdict,
                finalPosition: _season.IsComplete ? position : 0);
        }

        // Everything in `players` that is not in `other`, by id — the summer's ins and outs.
        private static List<Player> Missing(IReadOnlyList<Player> players, IReadOnlyList<Player> other)
        {
            var otherIds = new HashSet<int>();
            for (int i = 0; i < other.Count; i++)
            {
                otherIds.Add(other[i].Id.Value);
            }

            var missing = new List<Player>();
            for (int i = 0; i < players.Count; i++)
            {
                if (!otherIds.Contains(players[i].Id.Value))
                {
                    missing.Add(players[i]);
                }
            }

            return missing;
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
