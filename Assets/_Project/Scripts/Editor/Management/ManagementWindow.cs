using System;
using System.Collections.Generic;
using System.IO;
using Gaffer.Application.Drama;
using Gaffer.Application.Progression;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;
using Gaffer.Editor.Balance;
using Gaffer.Editor.Content;
using Gaffer.Editor.Harness;
using Gaffer.Infrastructure.Configuration;
using Gaffer.UserData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Position = Gaffer.Domain.Players.Position;

namespace Gaffer.Editor.Management
{
    /// <summary>
    /// The unified management bench: play a club through a league season week by week AND run its transfers in
    /// the same window. Wages drain the transfer cash every week (GDD §4.4), a signing or sale updates the live
    /// squad, strength and eleven the same week, drama interrupts the run for a decision, and the market is only
    /// open in the summer and winter windows. Styled in the ART_STYLE broadcast identity. Not shipped; a preview
    /// of the run the real UI (Faz 7) will present.
    ///
    /// <para><b>A view over <see cref="RunSession"/>, and nothing more.</b> This window used to own the run: its
    /// own league, season, simulator, drama engine, market, wage bill and rollover — a second copy of the flow
    /// that had already drifted from the Season Player window's. It now sends one command per action and replays
    /// the outcome it gets back (ARCHITECTURE §8, NON-NEGOTIABLE #4). It never reaches into the core to work out
    /// what changed: the league position, the losing streak and the final standing are read off
    /// <see cref="WeekOutcome"/>, the ATK/MID/DEF pills and the chance profile off <see cref="LineupOutcome"/>
    /// (both derived through the run's <em>own</em> trait catalog and tactics balance, so they cannot disagree
    /// with the sim the way a locally-built <c>EffectiveStrengthBuilder</c> did), and the summer's ins and outs
    /// off <see cref="SeasonRollover"/>.</para>
    /// </summary>
    public sealed class ManagementWindow : EditorWindow
    {
        private int _teamCount = 20;
        private long _seed = 20260709L;
        private int _managedIndex = 15;
        private int _promotionPosition = 3;
        private int _survivalPosition = 17;

        private long _startingCash = 6_000_000L;
        private long _wageBudget = 160_000L;
        private int _marketSize = 40;
        private int _gems = 3;
        private float _accuracy = 0.3f;
        private bool _reveal;

        // The shape and setup the run is on. Held here as well as in the session so a new run — and a resumed
        // one, whose save carries neither — starts on what the manager last chose. Re-synced from every outcome.
        private Tactics _tactics = Tactics.Balanced;
        private Formation _formation = Formation.F442;

        // ----- The run, and the outcomes last replayed from it ------------------------------------------
        private RunSession _session;
        private LineupOutcome _lineup;
        private WeekOutcome _lastWeek;
        private SeasonRollover _summer;

        private int _dragFromSlot = -1;
        private int _dragPlayerId = -1;
        private string _lineupStatus;
        private VisualElement _dragGhost;
        private readonly List<VisualElement> _slotTokens = new List<VisualElement>();

        private string _transferStatus;

        // How much weekly wage ceiling the budget-exchange row is set to move, and what the last move (or
        // refusal) said. The amount is a window setting, not run state: the session owns the money.
        private long _budgetShiftWeekly = 5_000L;
        private string _budgetStatus;

        private PlayerRole? _marketRoleFilter;
        private int _marketMinAge = 15;
        private int _marketMaxAge = 40;

        // The run's market, strongest first, and then the slice the filters leave — the ListView's source.
        // Both are fields refilled only when something behind them moves, so a Refresh never re-sorts and
        // never re-rates the pool.
        private readonly List<Player> _marketOrder = new List<Player>();
        private readonly List<Player> _marketShown = new List<Player>();
        private IReadOnlyList<Player> _orderedFrom;
        private int _orderedCount = -1;

        // Live handles into the transfer card, for the repaints that must not rebuild the body. Dropped at
        // the top of Refresh, which clears the hierarchy they live in.
        private ListView _marketList;
        private Label _marketHeading;
        private Label _marketMask;
        private Label _marketEmpty;

        private string _saveStatus;
        private string _dramaStatus;

        // What the last answered drama actually did, replayed off its DramaResolution and kept on screen
        // until the next drama or the next season. _previewScratch is the decision card's working buffer,
        // refilled per choice while the card is built and never held past it.
        private readonly List<DramaLine> _aftermath = new List<DramaLine>();
        private readonly List<DramaLine> _previewScratch = new List<DramaLine>();
        private string _aftermathTitle;

        private SimulationBalanceSO _simulationBalance;
        private DevelopmentBalanceSO _developmentBalance;
        private RenewalBalanceSO _renewalBalance;
        private EconomyBalanceSO _economyBalance;
        private DramaBalanceSO _dramaBalance;
        private ScoutingBalanceSO _scoutingBalance;
        private TraitCatalogSO _traitCatalog;
        private DramaCatalogSO _dramaCatalog;

        private VisualElement _body;

        private static string SavePath => Path.Combine(UnityEngine.Application.persistentDataPath, "gaffer-run.json");

        [MenuItem("Gaffer/Management")]
        public static void ShowWindow()
        {
            ManagementWindow window = GetWindow<ManagementWindow>();
            window.titleContent = new GUIContent("Management");
            window.minSize = new Vector2(600, 760);
        }

        public void CreateGUI()
        {
            _simulationBalance = _simulationBalance != null ? _simulationBalance : BalanceAssets.Simulation();
            _developmentBalance = _developmentBalance != null ? _developmentBalance : BalanceAssets.Development();
            _renewalBalance = _renewalBalance != null ? _renewalBalance : BalanceAssets.Renewal();
            _dramaBalance = _dramaBalance != null ? _dramaBalance : BalanceAssets.Drama();
            _economyBalance = _economyBalance != null ? _economyBalance : BalanceAssets.Economy();
            _scoutingBalance = _scoutingBalance != null ? _scoutingBalance : BalanceAssets.Scouting();
            _traitCatalog = _traitCatalog != null ? _traitCatalog : ContentAssets.Traits();
            _dramaCatalog = _dramaCatalog != null ? _dramaCatalog : ContentAssets.Drama();

            var scroll = new ScrollView();
            scroll.style.backgroundColor = HarnessPalette.Pitch;
            rootVisualElement.Add(scroll);

            var page = new VisualElement();
            SetPadding(page, 20);
            scroll.Add(page);

            Label title = MakeLabel("MANAGEMENT", 22, HarnessPalette.Chalk, bold: true);
            title.style.letterSpacing = 2f;
            page.Add(title);
            page.Add(MakeLabel("Play the season and run the transfers in one window — wages bite every week", 11, HarnessPalette.Muted));

            page.Add(BuildSetup());

            _body = new VisualElement();
            _body.style.marginTop = 6;
            page.Add(_body);

            _body.Add(MakeCard());
            ((VisualElement)_body[0]).Add(MakeLabel("Set it up, then Start Season.", 12, HarnessPalette.Muted));
        }

        // The drag ghost is parented to the window root, outside the body that Refresh rebuilds, so a window
        // closed or reloaded mid-drag would otherwise leave it behind (UNITY.md §5).
        private void OnDisable()
        {
            EndGhost();
            _slotTokens.Clear();
        }

        private VisualElement BuildSetup()
        {
            VisualElement card = MakeCard();

            var teams = new IntegerField("Teams") { value = _teamCount };
            teams.RegisterValueChangedCallback(e => _teamCount = e.newValue);
            card.Add(teams);

            var seed = new LongField("Seed") { value = _seed };
            seed.RegisterValueChangedCallback(e => _seed = e.newValue);
            card.Add(seed);

            var managed = new IntegerField("Your club (index, 0 = strongest)") { value = _managedIndex };
            managed.RegisterValueChangedCallback(e => _managedIndex = e.newValue);
            card.Add(managed);

            var promotion = new IntegerField("Promotion by position") { value = _promotionPosition };
            promotion.RegisterValueChangedCallback(e => _promotionPosition = e.newValue);
            card.Add(promotion);

            var survival = new IntegerField("Survive by position") { value = _survivalPosition };
            survival.RegisterValueChangedCallback(e => _survivalPosition = e.newValue);
            card.Add(survival);

            Label money = MakeLabel("Economy", 10, HarnessPalette.Muted, bold: true);
            money.style.marginTop = 6;
            card.Add(money);

            var cash = new LongField("Transfer cash (€)") { value = _startingCash };
            cash.RegisterValueChangedCallback(e => _startingCash = e.newValue);
            card.Add(cash);

            var wageBudget = new LongField("Wage budget (€/wk)") { value = _wageBudget };
            wageBudget.RegisterValueChangedCallback(e => _wageBudget = e.newValue);
            card.Add(wageBudget);

            var marketSize = new IntegerField("Market size") { value = _marketSize };
            marketSize.RegisterValueChangedCallback(e => _marketSize = e.newValue);
            card.Add(marketSize);

            var gems = new IntegerField("Guaranteed gems in market") { value = _gems };
            gems.RegisterValueChangedCallback(e => _gems = e.newValue);
            card.Add(gems);

            // Dragging this fires every frame. It used to call Refresh(), which cleared the body and
            // rebuilt a row — and ran a scout report — for every prospect in the market. It now rebinds
            // the handful of rows the ListView has realized, and nothing else moves.
            var accuracy = new Slider("Scout accuracy", 0f, 1f) { value = _accuracy };
            accuracy.RegisterValueChangedCallback(e =>
            {
                _accuracy = e.newValue;
                RebindMarket();
            });
            card.Add(accuracy);

            var reveal = new Toggle("Reveal true potential (dev)") { value = _reveal };
            reveal.RegisterValueChangedCallback(e =>
            {
                _reveal = e.newValue;
                RebindMarket();
            });
            card.Add(reveal);

            card.Add(MakeLabel("Balance (optional — assign a Gaffer/Balance SO to retune)", 10, HarnessPalette.Muted));
            var simField = new ObjectField("Simulation balance") { objectType = typeof(SimulationBalanceSO), value = _simulationBalance };
            simField.RegisterValueChangedCallback(e => _simulationBalance = e.newValue as SimulationBalanceSO);
            card.Add(simField);
            var devField = new ObjectField("Development balance") { objectType = typeof(DevelopmentBalanceSO), value = _developmentBalance };
            devField.RegisterValueChangedCallback(e => _developmentBalance = e.newValue as DevelopmentBalanceSO);
            card.Add(devField);
            var renewField = new ObjectField("Renewal balance") { objectType = typeof(RenewalBalanceSO), value = _renewalBalance };
            renewField.RegisterValueChangedCallback(e => _renewalBalance = e.newValue as RenewalBalanceSO);
            card.Add(renewField);
            var dramaBalanceField = new ObjectField("Drama balance") { objectType = typeof(DramaBalanceSO), value = _dramaBalance };
            dramaBalanceField.RegisterValueChangedCallback(e => _dramaBalance = e.newValue as DramaBalanceSO);
            card.Add(dramaBalanceField);
            var economyField = new ObjectField("Economy balance") { objectType = typeof(EconomyBalanceSO), value = _economyBalance };
            economyField.RegisterValueChangedCallback(e => _economyBalance = e.newValue as EconomyBalanceSO);
            card.Add(economyField);
            var scoutingField = new ObjectField("Scouting balance") { objectType = typeof(ScoutingBalanceSO), value = _scoutingBalance };
            scoutingField.RegisterValueChangedCallback(e => _scoutingBalance = e.newValue as ScoutingBalanceSO);
            card.Add(scoutingField);

            card.Add(MakeLabel("Content (optional — assign Gaffer/Content catalogs to override the built-ins)", 10, HarnessPalette.Muted));
            var traitCatalogField = new ObjectField("Trait catalog") { objectType = typeof(TraitCatalogSO), value = _traitCatalog };
            traitCatalogField.RegisterValueChangedCallback(e => _traitCatalog = e.newValue as TraitCatalogSO);
            card.Add(traitCatalogField);
            var dramaCatalogField = new ObjectField("Drama catalog") { objectType = typeof(DramaCatalogSO), value = _dramaCatalog };
            dramaCatalogField.RegisterValueChangedCallback(e => _dramaCatalog = e.newValue as DramaCatalogSO);
            card.Add(dramaCatalogField);

            var start = new Button(StartSeason) { text = "Start Season" };
            start.style.backgroundColor = HarnessPalette.Accent;
            start.style.color = HarnessPalette.Pitch;
            start.style.unityFontStyleAndWeight = FontStyle.Bold;
            start.style.height = 30;
            start.style.marginTop = 10;
            SetRadius(start, 6);
            card.Add(start);

            var load = new Button(LoadRun) { text = "Load Saved Run" };
            load.style.height = 24;
            load.style.marginTop = 6;
            SetRadius(load, 5);
            card.Add(load);

            return card;
        }

        // ----- The run's wiring seam -----------------------------------------------------------------------

        // The setup knobs as one value object. Formation and tactics ride along so a resumed run — whose save
        // carries neither — continues on the shape the manager was playing.
        private RunSetup Setup()
        {
            return new RunSetup(
                teamCount: _teamCount,
                seed: (ulong)_seed,
                managedClubIndex: _managedIndex,
                promotionPosition: _promotionPosition,
                survivalPosition: _survivalPosition,
                startingCash: _startingCash,
                weeklyWageBudget: _wageBudget,
                marketSize: _marketSize,
                guaranteedGems: _gems,
                formation: _formation,
                tactics: _tactics);
        }

        // Every tuning object and catalog the run plays on, from the assigned config assets or the calibrated
        // defaults. One bundle handed to the factory, so the league generator and the season cannot end up on
        // different trait catalogs the way two hand-wired windows did (ARCHITECTURE §6).
        //
        // Authored catalogs come through the VALIDATING loaders, and a refused load refuses the run: this
        // returns a Result so the caller reports the problem on the status line instead of starting a season
        // on content whose trait references do not resolve (NON-NEGOTIABLE #7). The drama catalog is checked
        // against the trait catalog THIS run plays with — a slug is only dangling relative to a specific
        // trait set — which is why the traits are loaded first.
        private Result<RunBalance> Balance()
        {
            Gaffer.Domain.Traits.TraitCatalog traits = Gaffer.Domain.Traits.TraitCatalog.Default;
            if (_traitCatalog != null)
            {
                Result<Gaffer.Domain.Traits.TraitCatalog> loadedTraits = _traitCatalog.Load();
                if (loadedTraits.IsFailure)
                {
                    return Result<RunBalance>.Failure(loadedTraits.Error);
                }

                traits = loadedTraits.Value;
            }

            Gaffer.Domain.Drama.DramaCatalog dramaEvents = Gaffer.Domain.Drama.DramaCatalog.Default;
            if (_dramaCatalog != null)
            {
                Result<Gaffer.Domain.Drama.DramaCatalog> loadedDrama = _dramaCatalog.Load(traits);
                if (loadedDrama.IsFailure)
                {
                    return Result<RunBalance>.Failure(loadedDrama.Error);
                }

                dramaEvents = loadedDrama.Value;
            }

            return Result<RunBalance>.Success(new RunBalance(
                simulation: _simulationBalance != null ? _simulationBalance.ToSettings() : MatchSimulationSettings.Default,
                tacticsBalance: _simulationBalance != null ? _simulationBalance.ToTacticsSettings() : TacticsSettings.Default,
                scorer: _simulationBalance != null ? _simulationBalance.ToScorerWeights() : ScorerWeights.Default,
                development: _developmentBalance != null ? _developmentBalance.ToSettings() : DevelopmentSettings.Default,
                renewal: _renewalBalance != null ? _renewalBalance.ToSettings() : RenewalSettings.Default,
                drama: _dramaBalance != null ? _dramaBalance.ToSettings() : DramaSettings.Default,
                morale: _dramaBalance != null ? _dramaBalance.ToMoraleSettings() : MoraleSettings.Default,
                economy: _economyBalance != null ? _economyBalance.ToSettings() : EconomySettings.Default,
                scouting: _scoutingBalance != null ? _scoutingBalance.ToSettings() : ScoutingSettings.Default,
                traits: traits,
                dramaEvents: dramaEvents));
        }

        private void StartSeason()
        {
            Result<RunBalance> balance = Balance();
            if (balance.IsFailure)
            {
                _saveStatus = balance.Error;
                Refresh();
                return;
            }

            Result<RunSession> started = RunSessionFactory.Start(Setup(), balance.Value);
            if (started.IsFailure)
            {
                _saveStatus = started.Error;
                Refresh();
                return;
            }

            Adopt(started.Value, null);
        }

        private void Adopt(RunSession session, string status)
        {
            _session = session;

            // A different run's market is a different list; forget what the old one was ordered from so
            // the watch in SyncMarketOrder cannot mistake it for unchanged.
            _orderedFrom = null;
            _orderedCount = -1;

            Replay(session.Lineup());
            _lastWeek = null;
            _summer = null;
            _lineupStatus = null;
            _transferStatus = null;
            _budgetStatus = null;
            _dramaStatus = null;
            ClearAftermath();
            _saveStatus = status;
            Refresh();
        }

        // The one place a team sheet is taken from an outcome. Shape and tactics are read back off it rather
        // than assumed, so what the setup would restart on is always what the run is actually playing.
        private void Replay(LineupOutcome lineup)
        {
            if (lineup == null)
            {
                return;
            }

            _lineup = lineup;
            _formation = lineup.Formation;
            _tactics = lineup.Tactics;
        }

        private void Apply(Result<LineupOutcome> result)
        {
            if (result.IsFailure)
            {
                _lineupStatus = result.Error;
            }
            else
            {
                _lineupStatus = null;
                Replay(result.Value);
            }

            Refresh();
        }

        private void SaveRun()
        {
            if (_session == null)
            {
                _saveStatus = "Start a season before saving.";
                Refresh();
                return;
            }

            Result result = SaveStore().Save(SavePath, _session.Capture());
            _saveStatus = result.IsSuccess ? "Saved run to " + SavePath : result.Error;
            Refresh();
        }

        // Reads the run back and resumes it on a SESSION-FRESH seed, so the unplayed fixtures are not the
        // ones the save was heading for — the same eleven can lose the match it won before the reload
        // (RunSessionFactory.Resume documents the trade, including that this makes save-scumming possible).
        // The run itself — money, tactics, the team sheet, the market, morale, drama — comes out of the
        // document from schema v6, not out of this window's fields.
        private void LoadRun()
        {
            Result<SeasonSaveData> loaded = SaveStore().Load(SavePath);
            if (loaded.IsFailure)
            {
                _saveStatus = loaded.Error;
                Refresh();
                return;
            }

            Result<RunBalance> balance = Balance();
            if (balance.IsFailure)
            {
                _saveStatus = balance.Error;
                Refresh();
                return;
            }

            Result<RunSession> resumed = RunSessionFactory.Resume(
                Setup(), balance.Value, loaded.Value, Gaffer.Infrastructure.Persistence.ContinuationSeed.Fresh());
            if (resumed.IsFailure)
            {
                _saveStatus = resumed.Error;
                Refresh();
                return;
            }

            // The two seeds, printed because they are what a bug report needs: the original one rebuilds
            // this world from nothing, and the continuation one replays the future this load just rolled.
            // Pass the second back to Resume and the same weeks come out again.
            Adopt(resumed.Value, "Loaded run from " + SavePath +
                " (world seed " + resumed.Value.OriginalSeed + ", continuing on " + resumed.Value.Seed + ")");
        }

        private static JsonSaveStore SaveStore()
        {
            return new JsonSaveStore(new NewtonsoftJsonSerializer(), new SaveMigrator());
        }

        // ----- Transfers -----------------------------------------------------------------------------------

        private void Sign(Player player)
        {
            Result<TransferOutcome> result = _session.SignPlayer(player);
            if (result.IsFailure)
            {
                _transferStatus = result.Error;
            }
            else
            {
                TransferOutcome outcome = result.Value;
                Replay(outcome.Lineup);
                _transferStatus = "Signed " + outcome.Player.Name + " for " + FormatValue(outcome.Fee) +
                    " (" + FormatValue(outcome.WeeklyWage) + "/wk).";
            }

            Refresh();
        }

        private void Sell(Player player)
        {
            Result<TransferOutcome> result = _session.SellPlayer(player);
            if (result.IsFailure)
            {
                _transferStatus = result.Error;
            }
            else
            {
                TransferOutcome outcome = result.Value;
                Replay(outcome.Lineup);
                _transferStatus = "Sold " + outcome.Player.Name + " for " + FormatValue(outcome.Fee) + ".";
            }

            Refresh();
        }

        // Moves money between the two budgets. Negative gives up wage ceiling for cash, positive buys
        // ceiling with cash; the session refuses what cannot happen and the refusal is shown as it came.
        // No window gate here — the exchange moves no player, so it is live all season.
        private void ShiftBudget(long weeklyDelta)
        {
            Result<BudgetShiftOutcome> result = _session.ShiftWageBudget(weeklyDelta);
            if (result.IsFailure)
            {
                _budgetStatus = result.Error;
            }
            else
            {
                // Replayed off the outcome, not re-read from the session (ARCHITECTURE §8).
                BudgetShiftOutcome outcome = result.Value;
                _budgetStatus = (outcome.WeeklyWageBudgetDelta < 0 ? "Gave up " : "Bought ") +
                    FormatValue(outcome.WeeklyWageBudgetDelta < 0 ? -outcome.WeeklyWageBudgetDelta : outcome.WeeklyWageBudgetDelta) +
                    "/wk of ceiling for " + HarnessMoney.Signed(outcome.CashDelta) + " — now " +
                    FormatValue(outcome.Finances.Cash) + " cash and a " +
                    FormatValue(outcome.Finances.WeeklyWageBudget) + "/wk ceiling.";
            }

            Refresh();
        }

        // ----- Lineup --------------------------------------------------------------------------------------

        private bool IsStarting(int playerId)
        {
            return SlotOf(playerId) >= 0;
        }

        private int SlotOf(int playerId)
        {
            IReadOnlyList<Player> slots = _lineup != null ? _lineup.Slots : null;
            if (slots == null)
            {
                return -1;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Id.Value == playerId)
                {
                    return i;
                }
            }

            return -1;
        }

        private Player FindInSquad(int playerId)
        {
            Squad squad = _session != null ? _session.Squad : null;
            if (squad == null)
            {
                return null;
            }

            IReadOnlyList<Player> players = squad.Players;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Id.Value == playerId)
                {
                    return players[i];
                }
            }

            return null;
        }

        private void ChangeFormation(string formationName)
        {
            IReadOnlyList<Formation> presets = Formation.Presets;
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i].Name == formationName)
                {
                    Apply(_session.SetFormation(presets[i]));
                    return;
                }
            }
        }

        private void ChangeTactics(Tactics tactics)
        {
            Apply(_session.SetTactics(tactics));
        }

        // ----- Season loop ---------------------------------------------------------------------------------

        private void AdvanceOneWeek()
        {
            Result<WeekOutcome> week = _session.AdvanceWeek();
            if (week.IsFailure)
            {
                _dramaStatus = week.Error;
                Refresh();
                return;
            }

            ReplayWeek(week.Value);
            Refresh();
        }

        private void PlayToEnd()
        {
            Result<IReadOnlyList<WeekOutcome>> weeks = _session.AdvanceToEndOfSeason();
            if (weeks.IsFailure)
            {
                _dramaStatus = weeks.Error;
                Refresh();
                return;
            }

            IReadOnlyList<WeekOutcome> played = weeks.Value;
            for (int i = 0; i < played.Count; i++)
            {
                ReplayWeek(played[i]);
            }

            Refresh();
        }

        // Everything this week changed is on the outcome — the table position, the losing run, the wage
        // payment, the verdict. The window used to walk the season's live table and result history to work
        // these out for itself, which is the diff ARCHITECTURE §8 forbids: it could disagree with the state
        // the drama engine had already been ticked on.
        private void ReplayWeek(WeekOutcome week)
        {
            if (week.Matches.Count > 0)
            {
                _lastWeek = week;
            }
        }

        private void StartNextSeason()
        {
            Result<SeasonRollover> rolled = _session.StartNextSeason();
            if (rolled.IsFailure)
            {
                _saveStatus = rolled.Error;
                Refresh();
                return;
            }

            _summer = rolled.Value;
            Replay(_summer.Lineup);
            _lastWeek = null;
            _transferStatus = null;
            _budgetStatus = null;
            _dramaStatus = null;
            ClearAftermath();
            Refresh();
        }

        // ----- Drama ---------------------------------------------------------------------------------------

        private void ResolveDrama(int choiceIndex)
        {
            // Read before the answer: the pending event is gone the moment the session resolves it, and the
            // aftermath headline wants the words that were on the button the manager pressed.
            PendingDrama answered = _session.PendingDrama;

            Result<DramaResolution> result = _session.ResolveDrama(choiceIndex);
            if (result.IsFailure)
            {
                _dramaStatus = result.Error;
                Refresh();
                return;
            }

            DramaResolution resolution = result.Value;
            Replay(resolution.Lineup);
            _dramaStatus = null;
            RecordAftermath(answered, resolution);
            Refresh();
        }

        // The resolution replayed as concrete copy — the morale entries that landed and on whom, the cash
        // that moved and what is left, the sale that went through, the trait that passed on. Every one of
        // those is a field on the record, so nothing is inferred by comparing before and after (§8).
        private void RecordAftermath(PendingDrama answered, DramaResolution resolution)
        {
            string answer = answered != null && resolution.ChoiceIndex >= 0
                && resolution.ChoiceIndex < answered.Event.Choices.Count
                ? HarnessDrama.ChoiceLabel(answered.Event.Choices[resolution.ChoiceIndex], answered, _session)
                : null;

            // The event's written headline, not its id humanized — the same words that were on the card a
            // second ago, so the aftermath reads as the end of the story and not as a log line about it.
            string headline = answered != null
                ? HarnessDrama.Title(answered, _session)
                : HarnessDrama.Humanize(resolution.EventId.Value);
            _aftermathTitle = headline + (answer != null ? "  —  you chose: " + answer : " — resolved.");
            HarnessDrama.Aftermath(resolution, _session, _aftermath);
        }

        private void ClearAftermath()
        {
            _aftermath.Clear();
            _aftermathTitle = null;
        }

        private VisualElement BuildDramaCard(PendingDrama pending)
        {
            VisualElement card = MakeCard();
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = HarnessPalette.Accent;

            card.Add(MakeLabel("DRAMA · WEEK " + _session.PlayedRounds, 11, HarnessPalette.Accent, bold: true));

            // The written headline, in the case it was written in. It is deliberately NOT upper-cased any
            // more: ToUpperInvariant maps Turkish 'i' to 'I' instead of 'İ', so a Turkish headline would be
            // misspelled by the view — and a headline reads better in its own case anyway.
            card.Add(MakeLabel(HarnessDrama.Title(pending, _session), 15, HarnessPalette.Chalk, bold: true));

            if (pending.Subject != null)
            {
                Player subject = pending.Subject;
                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;
                line.Add(MakeLabel(
                    subject.Name + "  ·  " + HarnessLabels.RoleLabel(subject.Role) + "  ·  " + subject.Age +
                    "  ·  OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(subject)), 11, HarnessPalette.Chalk));
                line.Add(HarnessMorale.MakeBadgeFor(_session.MoralePointsOf(subject.Id)));
                line.Add(TraitBadges(subject));
                card.Add(line);
            }

            // What happened. BodyKey has existed on DramaEvent since it was written and no window has ever
            // drawn it; the effect preview below is the consequence, this is the event.
            Label body = MakeLabel(HarnessDrama.Body(pending, _session), 12, HarnessPalette.Chalk);
            body.style.marginTop = 6;
            card.Add(body);

            card.Add(MakeLabel(
                "Every answer's consequences are listed under it — real numbers, priced against this squad and these books.",
                10, HarnessPalette.Muted));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.alignItems = Align.FlexStart;
            buttons.style.marginTop = 8;
            for (int i = 0; i < pending.Event.Choices.Count; i++)
            {
                buttons.Add(BuildChoiceColumn(pending, i));
            }

            card.Add(buttons);
            return card;
        }

        // One answer: the verb on the button, and under it every effect the choice carries, one line each.
        // The columns share the width evenly (flexBasis 0 + flexGrow 1) and the lines wrap rather than clip,
        // so three answers side by side stay readable and nothing is silently truncated.
        private VisualElement BuildChoiceColumn(PendingDrama pending, int index)
        {
            DramaChoice choice = pending.Event.Choices[index];

            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.flexShrink = 1;
            column.style.flexBasis = 0;
            if (index > 0)
            {
                column.style.marginLeft = 6;
            }

            var button = new Button(() => ResolveDrama(index)) { text = HarnessDrama.ChoiceLabel(choice, pending, _session) };

            // minHeight, not height: the label wraps at three columns wide, and a fixed height would clip
            // the second line of a long answer rather than grow for it.
            button.style.minHeight = 26;
            button.style.fontSize = 11;
            button.style.whiteSpace = WhiteSpace.Normal;
            button.style.marginLeft = 0;
            button.style.marginRight = 0;
            button.style.backgroundColor = HarnessPalette.Accent;
            button.style.color = HarnessPalette.Pitch;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetRadius(button, 6);
            column.Add(button);

            var panel = new VisualElement();
            panel.style.marginTop = 4;
            panel.style.backgroundColor = HarnessPalette.Pitch;
            SetBorder(panel, HarnessPalette.PitchLine, 1);
            SetRadius(panel, 6);
            SetPadding(panel, 8);
            column.Add(panel);

            HarnessDrama.Preview(pending, choice, _session, _previewScratch);
            for (int i = 0; i < _previewScratch.Count; i++)
            {
                Label line = MakeLabel(_previewScratch[i].Text, 10, _previewScratch[i].Tone);
                if (i > 0)
                {
                    line.style.marginTop = 3;
                }

                panel.Add(line);
            }

            return column;
        }

        // What the last answer did, in the same terms the preview promised it in.
        private VisualElement BuildAftermathCard()
        {
            VisualElement card = MakeCard();
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = HarnessPalette.Draw;

            card.Add(MakeLabel("WHAT YOUR ANSWER DID", 11, HarnessPalette.Draw, bold: true));
            card.Add(MakeLabel(_aftermathTitle, 13, HarnessPalette.Chalk, bold: true));
            for (int i = 0; i < _aftermath.Count; i++)
            {
                Label line = MakeLabel(_aftermath[i].Text, 11, _aftermath[i].Tone);
                line.style.marginTop = 3;
                card.Add(line);
            }

            return card;
        }

        // ----- Render --------------------------------------------------------------------------------------

        private void Refresh()
        {
            // The body is about to be cleared, so the handles into it go first: a ListView left in a field
            // after its hierarchy was dropped would be rebound into nothing (UNITY.md §5).
            _marketList = null;
            _marketHeading = null;
            _marketMask = null;
            _marketEmpty = null;

            _body.Clear();
            if (_session == null)
            {
                return;
            }

            VisualElement header = MakeCard();
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.Add(MakeLabel("YOU MANAGE  " + _session.ManagedClubName.ToUpperInvariant(), 13, HarnessPalette.Accent, bold: true));
            top.Add(MakeLabel(
                "Season " + _session.SeasonNumber + "  ·  Week " + _session.PlayedRounds + " / " + _session.RoundCount,
                12, HarnessPalette.Muted));
            header.Add(top);

            BoardTarget target = _session.BoardTarget;
            header.Add(MakeLabel(
                "Board target: finish top " + target.PromotionPosition + " to go up, stay above " +
                target.SurvivalPosition + " to keep your job.", 11, HarnessPalette.Muted));

            // Where the run stands, replayed from the last week's outcome rather than re-derived from the
            // table — the core already worked it out, and the drama engine was ticked on that same answer.
            if (_lastWeek != null)
            {
                header.Add(MakeLabel(
                    "Position " + _lastWeek.TablePosition + "  ·  " + FormOf(_lastWeek.LossStreak) +
                    "  ·  wages " + FormatValue(_lastWeek.WagesPaid) + " paid last week",
                    10, _lastWeek.LossStreak >= 3 ? HarnessPalette.Loss : HarnessPalette.Muted));
            }

            Finances finances = _session.Finances;
            var moneyRow = new VisualElement();
            moneyRow.style.flexDirection = FlexDirection.Row;
            moneyRow.style.justifyContent = Justify.SpaceBetween;
            moneyRow.style.marginTop = 8;
            Color cashColor = finances.Cash < 0 ? HarnessPalette.Loss : HarnessPalette.Accent;
            moneyRow.Add(MakeLabel("CASH  " + FormatValue(finances.Cash), 15, cashColor, bold: true));
            Color wageColor = finances.WageHeadroom < 0 ? HarnessPalette.Loss : HarnessPalette.Muted;
            moneyRow.Add(MakeLabel(
                "Wages " + FormatValue(finances.WeeklyWageBill) + " / " + FormatValue(finances.WeeklyWageBudget) +
                "/wk  ·  " + FormatValue(finances.WageHeadroom) + "/wk free", 11, wageColor));
            header.Add(moneyRow);
            header.Add(BuildBudgetExchange());

            var saveRow = new VisualElement();
            saveRow.style.flexDirection = FlexDirection.Row;
            saveRow.style.marginTop = 8;
            var save = new Button(SaveRun) { text = "Save Run" };
            save.style.height = 22;
            save.style.flexGrow = 1;
            SetRadius(save, 5);
            saveRow.Add(save);
            var reload = new Button(LoadRun) { text = "Load Run" };
            reload.style.height = 22;
            reload.style.flexGrow = 1;
            reload.style.marginLeft = 6;
            SetRadius(reload, 5);
            saveRow.Add(reload);
            header.Add(saveRow);

            if (!string.IsNullOrEmpty(_saveStatus))
            {
                header.Add(MakeLabel(_saveStatus, 10, HarnessPalette.Muted));
            }

            _body.Add(header);

            PendingDrama pending = _session.PendingDrama;
            if (pending != null)
            {
                // A raised event blocks the week until answered — drama is a decision, not a notification.
                _body.Add(BuildDramaCard(pending));
            }
            else if (!_session.IsSeasonComplete)
            {
                var controls = new VisualElement();
                controls.style.flexDirection = FlexDirection.Row;
                controls.style.marginTop = 8;

                var advance = new Button(AdvanceOneWeek) { text = "Advance Week  ·  pay wages" };
                advance.style.backgroundColor = HarnessPalette.Accent;
                advance.style.color = HarnessPalette.Pitch;
                advance.style.unityFontStyleAndWeight = FontStyle.Bold;
                advance.style.flexGrow = 1;
                advance.style.height = 28;
                SetRadius(advance, 6);
                controls.Add(advance);

                var playToEnd = new Button(PlayToEnd) { text = "Play to End" };
                playToEnd.style.flexGrow = 1;
                playToEnd.style.height = 28;
                playToEnd.style.marginLeft = 6;
                controls.Add(playToEnd);

                _body.Add(controls);
            }
            else
            {
                _body.Add(BuildVerdictBanner());

                var next = new Button(StartNextSeason) { text = "Start Next Season  →  age + develop + renew every squad" };
                next.style.backgroundColor = HarnessPalette.Accent;
                next.style.color = HarnessPalette.Pitch;
                next.style.unityFontStyleAndWeight = FontStyle.Bold;
                next.style.height = 28;
                next.style.marginTop = 8;
                SetRadius(next, 6);
                _body.Add(next);
            }

            if (!string.IsNullOrEmpty(_dramaStatus) && pending == null)
            {
                Label dramaNote = MakeLabel(_dramaStatus, 10, HarnessPalette.Draw);
                dramaNote.style.marginTop = 4;
                _body.Add(dramaNote);
            }

            // The last answer's consequences stay on screen until the next drama or the next season — the
            // morale it left is on the squad rows below for as many weeks as it lasts.
            if (pending == null && _aftermath.Count > 0)
            {
                _body.Add(BuildAftermathCard());
            }

            if (_summer != null && (_summer.Retired.Count > 0 || _summer.Arrived.Count > 0))
            {
                _body.Add(BuildSummerCard());
            }

            _body.Add(BuildLineupCard());
            _body.Add(BuildTacticsCard());
            _body.Add(BuildSquadCard());
            _body.Add(BuildTransferCard());
            _body.Add(BuildTableCard());

            if (_lastWeek != null)
            {
                _body.Add(BuildLastWeekCard());
            }
        }

        /// <summary>
        /// The board's standing offer to move money between the two budgets, sitting under the line that
        /// shows them. Both directions are priced by the core <em>before</em> either button is pressed:
        /// the buttons carry the trade and the two lines under them say what the books would look like
        /// afterwards, or — quoted from the session, word for word — why that direction is refused. The
        /// owner's complaint was cash he could not spend and no way to see what would happen until he had
        /// already acted; nothing here waits for a click to tell him.
        ///
        /// <para>The figures come from <c>RunSession.PreviewWageBudgetShift</c>, which runs the very
        /// method the click runs without committing it, so the preview and the answer cannot disagree.</para>
        /// </summary>
        private VisualElement BuildBudgetExchange()
        {
            var block = new VisualElement();
            block.style.marginTop = 8;
            block.Add(MakeLabel(
                "REBALANCE THE BUDGETS  ·  " + FormatValue(1) + "/wk of ceiling ⇄ " +
                FormatValue(_session.WageBudgetExchangeWeeks) + " cash, both ways",
                10, HarnessPalette.Muted, bold: true));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 4;

            var amount = new LongField("Move (€/wk)") { value = _budgetShiftWeekly };
            amount.style.width = 170;
            amount.style.marginRight = 6;
            row.Add(amount);

            var give = new Button(() => ShiftBudget(-BudgetShiftMagnitude()));
            var buy = new Button(() => ShiftBudget(BudgetShiftMagnitude()));
            StyleExchangeButton(give);
            StyleExchangeButton(buy);
            row.Add(give);
            row.Add(buy);
            block.Add(row);

            Label giveLine = MakeLabel(string.Empty, 10, HarnessPalette.Muted);
            giveLine.style.marginTop = 3;
            block.Add(giveLine);
            Label buyLine = MakeLabel(string.Empty, 10, HarnessPalette.Muted);
            block.Add(buyLine);

            // Repriced on every keystroke: nothing is created or destroyed, only text and colour written,
            // so it is cheap enough to run while the amount is being typed (the accuracy slider's rule).
            void Paint()
            {
                long weekly = BudgetShiftMagnitude();
                if (weekly == 0)
                {
                    ShowExchangeIdle(give, giveLine, "Free wage room", "Type a weekly amount to give up for cash.");
                    ShowExchangeIdle(buy, buyLine, "Buy wage room", "Type a weekly amount to buy with cash.");
                    return;
                }

                Finances money = _session.Finances;
                BudgetShiftVerdict giving = BudgetShiftVerdict.For(-weekly, money, _session.PreviewWageBudgetShift(-weekly));
                BudgetShiftVerdict buying = BudgetShiftVerdict.For(weekly, money, _session.PreviewWageBudgetShift(weekly));

                ShowExchangeOption(give, giveLine, giving.ActionLabel(), giving.Sentence(), giving.Allowed);
                ShowExchangeOption(buy, buyLine, buying.ActionLabel(), buying.Sentence(), buying.Allowed);
            }

            amount.RegisterValueChangedCallback(changed =>
            {
                _budgetShiftWeekly = changed.newValue;
                Paint();
            });

            Paint();

            if (!string.IsNullOrEmpty(_budgetStatus))
            {
                Label status = MakeLabel(_budgetStatus, 10, HarnessPalette.Chalk);
                status.style.marginTop = 3;
                block.Add(status);
            }

            return block;
        }

        // The typed amount as a positive weekly figure: the two buttons carry the direction, so a negative
        // entry means the same trade as the positive one rather than a silently inverted button.
        // long.MinValue has no positive twin, so it reads as nothing to move.
        private long BudgetShiftMagnitude()
        {
            if (_budgetShiftWeekly == long.MinValue)
            {
                return 0L;
            }

            return _budgetShiftWeekly < 0 ? -_budgetShiftWeekly : _budgetShiftWeekly;
        }

        private static void StyleExchangeButton(Button button)
        {
            button.style.flexGrow = 1;
            button.style.height = 22;
            button.style.marginLeft = 4;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            SetRadius(button, 5);
        }

        // A blocked direction keeps its button live, the way a market row does: the session still gets the
        // click and still writes the authoritative message, and the line only says in advance what that
        // message will be.
        private static void ShowExchangeOption(Button button, Label line, string action, string sentence, bool allowed)
        {
            button.text = action;
            button.SetEnabled(true);
            button.style.backgroundColor = allowed ? HarnessPalette.Accent : HarnessPalette.PitchLine;
            button.style.color = allowed ? HarnessPalette.Pitch : HarnessPalette.Muted;
            line.text = sentence;
            line.style.color = allowed ? HarnessPalette.Muted : HarnessPalette.Loss;
        }

        // No amount typed: there is no trade to attempt, so the buttons go quiet rather than sending the
        // session a no-op it would have to answer.
        private static void ShowExchangeIdle(Button button, Label line, string action, string hint)
        {
            button.text = action;
            button.SetEnabled(false);
            button.style.backgroundColor = HarnessPalette.PitchLine;
            button.style.color = HarnessPalette.Muted;
            line.text = hint;
            line.style.color = HarnessPalette.Muted;
        }

        private static string FormOf(int lossStreak)
        {
            if (lossStreak == 0)
            {
                return "no losing run";
            }

            return lossStreak == 1 ? "1 defeat on the bounce" : lossStreak + " defeats on the bounce";
        }

        private VisualElement BuildSummerCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("SUMMER " + _summer.SeasonNumber, 11, HarnessPalette.Muted, bold: true));

            if (_summer.Retired.Count > 0)
            {
                var outLine = new List<string>(_summer.Retired.Count);
                foreach (Player p in _summer.Retired)
                {
                    outLine.Add(p.Name + " (" + HarnessLabels.RoleLabel(p.Role) + " " + p.Age + ")");
                }

                card.Add(MakeLabel("Retired:  " + string.Join(",   ", outLine), 11, HarnessPalette.Loss));
            }

            if (_summer.Arrived.Count > 0)
            {
                var inLine = new List<string>(_summer.Arrived.Count);
                foreach (Player p in _summer.Arrived)
                {
                    int ovr = Mathf.RoundToInt((float)PlayerRatings.ForRole(p));
                    inLine.Add(p.Name + " (" + HarnessLabels.RoleLabel(p.Role) + " " + p.Age + ", OVR " + ovr + ")");
                }

                card.Add(MakeLabel("Youth in:  " + string.Join(",   ", inLine), 11, HarnessPalette.Accent));
            }

            return card;
        }

        private VisualElement BuildLineupCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("LINEUP", 11, HarnessPalette.Muted, bold: true));

            int starting = _lineup.Starters.Count;
            card.Add(MakeLabel(
                "Starting XI: " + starting + "/" + _lineup.Formation.Total + "   ·   drag players on the pitch, or up from the bench",
                10, _lineup.IsComplete ? HarnessPalette.Muted : HarnessPalette.Loss));

            if (!string.IsNullOrEmpty(_lineupStatus))
            {
                card.Add(MakeLabel(_lineupStatus, 10, HarnessPalette.Loss));
            }

            var names = new List<string>();
            foreach (Formation preset in Formation.Presets)
            {
                names.Add(preset.Name);
            }

            var formation = new DropdownField("Formation", names, IndexOfFormation());
            formation.RegisterValueChangedCallback(e => ChangeFormation(e.newValue));
            card.Add(formation);

            card.Add(BuildPitch());
            card.Add(BuildBench());

            return card;
        }

        private int IndexOfFormation()
        {
            IReadOnlyList<Formation> presets = Formation.Presets;
            for (int i = 0; i < presets.Count; i++)
            {
                if (presets[i].Name == _lineup.Formation.Name)
                {
                    return i;
                }
            }

            return 0;
        }

        private const float TokenWidth = 58f;
        private const float TokenHeight = 46f;

        private VisualElement BuildPitch()
        {
            var pitch = new VisualElement();
            pitch.style.height = 380;
            pitch.style.marginTop = 8;
            pitch.style.backgroundColor = HarnessPalette.Pitch;
            SetBorder(pitch, HarnessPalette.PitchLine, 1);
            SetRadius(pitch, 8);
            pitch.style.position = UnityEngine.UIElements.Position.Relative;
            pitch.style.overflow = Overflow.Hidden;

            var halfway = new VisualElement();
            halfway.style.position = UnityEngine.UIElements.Position.Absolute;
            halfway.style.left = 0;
            halfway.style.right = 0;
            halfway.style.top = Length.Percent(50);
            halfway.style.height = 1;
            halfway.style.backgroundColor = HarnessPalette.PitchLine;
            pitch.Add(halfway);

            _slotTokens.Clear();
            Formation shape = _lineup.Formation;
            IReadOnlyList<Player> slots = _lineup.Slots;
            Vector2[] positions = SlotPositions(shape);
            for (int i = 0; i < shape.Total; i++)
            {
                Player player = i < slots.Count ? slots[i] : null;
                VisualElement token = MakePitchToken(player, shape.Slots[i], i, positions[i]);
                _slotTokens.Add(token);
                pitch.Add(token);
            }

            return pitch;
        }

        private VisualElement MakePitchToken(Player player, PlayerRole slotRole, int slot, Vector2 pos)
        {
            var token = new VisualElement();
            token.style.position = UnityEngine.UIElements.Position.Absolute;
            token.style.left = Length.Percent(pos.x * 100f);
            token.style.top = Length.Percent(pos.y * 100f);
            token.style.translate = new Translate(Length.Percent(-50), Length.Percent(-50));
            token.style.width = TokenWidth;
            token.style.height = TokenHeight;
            token.style.alignItems = Align.Center;
            token.style.justifyContent = Justify.Center;
            SetRadius(token, 8);

            bool empty = player == null;
            token.style.backgroundColor = empty ? new Color(0, 0, 0, 0) : HarnessPalette.PitchRaised;
            SetBorder(token, empty ? HarnessPalette.PitchLine : HarnessPalette.Accent, empty ? 1 : 2);

            token.Add(MakeLabel(HarnessLabels.RoleLabel(slotRole), 9, HarnessPalette.Muted, bold: true));
            if (!empty)
            {
                var name = MakeLabel(Surname(player.Name), 10, HarnessPalette.Chalk, bold: true);
                name.style.unityTextAlign = TextAnchor.MiddleCenter;
                token.Add(name);
                token.Add(MakeLabel(Mathf.RoundToInt((float)PlayerRatings.ForRole(player)).ToString(), 9, HarnessPalette.Accent, bold: true));

                RegisterDrag(token, slot, player.Id.Value);
            }

            return token;
        }

        private VisualElement BuildBench()
        {
            var wrap = new VisualElement();
            wrap.style.marginTop = 8;
            wrap.Add(MakeLabel("BENCH", 10, HarnessPalette.Muted, bold: true));

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            foreach (Player player in _lineup.Bench)
            {
                var chip = new VisualElement();
                chip.style.flexDirection = FlexDirection.Row;
                chip.style.alignItems = Align.Center;
                chip.style.marginRight = 6;
                chip.style.marginTop = 4;
                chip.style.paddingLeft = 6;
                chip.style.paddingRight = 6;
                chip.style.height = 22;
                chip.style.backgroundColor = HarnessPalette.PitchRaised;
                SetBorder(chip, HarnessPalette.PitchLine, 1);
                SetRadius(chip, 6);
                chip.Add(MakeLabel(HarnessLabels.RoleLabel(player.Role) + " " + Surname(player.Name), 10, HarnessPalette.Muted));
                chip.Add(MakeLabel("  " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)), 10, HarnessPalette.Accent, bold: true));

                RegisterDrag(chip, -1, player.Id.Value);
                row.Add(chip);
            }

            wrap.Add(row);
            return wrap;
        }

        // Pointer-capture drag. The callbacks live exactly as long as the token they are on: every one of
        // these elements is discarded by the next Refresh's _body.Clear(), which is the removal UNITY.md §5
        // asks for — there is no longer-lived subject holding a reference to them.
        private void RegisterDrag(VisualElement token, int slot, int playerId)
        {
            token.RegisterCallback<PointerDownEvent>(evt =>
            {
                _dragFromSlot = slot;
                _dragPlayerId = playerId;
                token.CapturePointer(evt.pointerId);
                BeginGhost(playerId, evt.position);
                evt.StopPropagation();
            });

            token.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (token.HasPointerCapture(evt.pointerId))
                {
                    MoveGhost(evt.position);
                }
            });

            token.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (!token.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                token.ReleasePointer(evt.pointerId);
                EndGhost();
                HandleDrop(evt.position);
            });

            token.RegisterCallback<PointerCaptureOutEvent>(evt => EndGhost());
        }

        private void BeginGhost(int playerId, Vector2 position)
        {
            EndGhost();
            Player player = FindInSquad(playerId);
            if (player == null)
            {
                return;
            }

            _dragGhost = new VisualElement();
            _dragGhost.pickingMode = PickingMode.Ignore;
            _dragGhost.style.position = UnityEngine.UIElements.Position.Absolute;
            _dragGhost.style.width = TokenWidth;
            _dragGhost.style.height = TokenHeight;
            _dragGhost.style.alignItems = Align.Center;
            _dragGhost.style.justifyContent = Justify.Center;
            _dragGhost.style.opacity = 0.9f;
            _dragGhost.style.backgroundColor = HarnessPalette.PitchRaised;
            SetBorder(_dragGhost, HarnessPalette.Accent, 2);
            SetRadius(_dragGhost, 8);
            _dragGhost.Add(MakeLabel(HarnessLabels.RoleLabel(player.Role), 9, HarnessPalette.Muted, bold: true));
            _dragGhost.Add(MakeLabel(Surname(player.Name), 10, HarnessPalette.Chalk, bold: true));

            rootVisualElement.Add(_dragGhost);
            MoveGhost(position);
        }

        private void MoveGhost(Vector2 position)
        {
            if (_dragGhost == null)
            {
                return;
            }

            Vector2 local = rootVisualElement.WorldToLocal(position);
            _dragGhost.style.left = local.x - (TokenWidth / 2f);
            _dragGhost.style.top = local.y - (TokenHeight / 2f);
        }

        private void EndGhost()
        {
            if (_dragGhost != null)
            {
                _dragGhost.RemoveFromHierarchy();
                _dragGhost = null;
            }
        }

        // Dropping is two commands and nothing else: onto a slot is PlaceInSlot (the session decides whether
        // that is a swap or a promotion off the bench), off the pitch is ClearSlot.
        private void HandleDrop(Vector2 position)
        {
            int dropSlot = -1;
            for (int i = 0; i < _slotTokens.Count; i++)
            {
                if (i == _dragFromSlot)
                {
                    continue;
                }

                if (_slotTokens[i].worldBound.Contains(position))
                {
                    dropSlot = i;
                    break;
                }
            }

            int fromSlot = _dragFromSlot;
            int playerId = _dragPlayerId;
            _dragFromSlot = -1;
            _dragPlayerId = -1;

            if (dropSlot >= 0 && playerId >= 0)
            {
                Apply(_session.PlaceInSlot(dropSlot, new PlayerId(playerId)));
            }
            else if (dropSlot < 0 && fromSlot >= 0)
            {
                Apply(_session.ClearSlot(fromSlot));
            }
            else
            {
                _lineupStatus = null;
                Refresh();
            }
        }

        private static Vector2[] SlotPositions(Formation shape)
        {
            var bands = new Dictionary<float, List<int>>();
            var y = new float[shape.Total];
            for (int i = 0; i < shape.Total; i++)
            {
                y[i] = BandY(shape.Slots[i]);
                if (!bands.TryGetValue(y[i], out List<int> members))
                {
                    members = new List<int>();
                    bands[y[i]] = members;
                }

                members.Add(i);
            }

            var positions = new Vector2[shape.Total];
            foreach (KeyValuePair<float, List<int>> band in bands)
            {
                List<int> members = band.Value;
                members.Sort((a, b) =>
                {
                    int byWidth = HorizontalKey(shape.Slots[a]).CompareTo(HorizontalKey(shape.Slots[b]));
                    return byWidth != 0 ? byWidth : a.CompareTo(b);
                });

                for (int rank = 0; rank < members.Count; rank++)
                {
                    float x = (rank + 1f) / (members.Count + 1f);
                    positions[members[rank]] = new Vector2(x, band.Key);
                }
            }

            return positions;
        }

        private static float BandY(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return 0.90f;
                case PlayerRole.RightBack:
                case PlayerRole.CentreBack:
                case PlayerRole.LeftBack:
                    return 0.72f;
                case PlayerRole.DefensiveMidfield:
                    return 0.59f;
                case PlayerRole.RightMidfield:
                case PlayerRole.CentralMidfield:
                case PlayerRole.LeftMidfield:
                    return 0.47f;
                case PlayerRole.AttackingMidfield:
                    return 0.35f;
                default:
                    return 0.20f;
            }
        }

        private static int HorizontalKey(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.RightBack:
                case PlayerRole.RightMidfield:
                case PlayerRole.RightWing:
                    return 2;
                case PlayerRole.LeftBack:
                case PlayerRole.LeftMidfield:
                case PlayerRole.LeftWing:
                    return 0;
                default:
                    return 1;
            }
        }

        private static string Surname(string name)
        {
            int space = name.LastIndexOf(' ');
            return space >= 0 ? name.Substring(space + 1) : name;
        }

        private VisualElement BuildTacticsCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("TACTICS", 11, HarnessPalette.Muted, bold: true));
            card.Add(MakeLabel("Applies to your club from next week.", 10, HarnessPalette.Muted));

            Tactics current = _lineup.Tactics;

            var mentality = new EnumField("Mentality", current.Mentality);
            mentality.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics((Mentality)e.newValue, current.Tempo, current.Pressing, current.Approach)));
            card.Add(mentality);

            var tempo = new EnumField("Tempo", current.Tempo);
            tempo.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics(current.Mentality, (Tempo)e.newValue, current.Pressing, current.Approach)));
            card.Add(tempo);

            var pressing = new EnumField("Pressing", current.Pressing);
            pressing.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics(current.Mentality, current.Tempo, (Pressing)e.newValue, current.Approach)));
            card.Add(pressing);

            var approach = new EnumField("Approach", current.Approach);
            approach.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics(current.Mentality, current.Tempo, current.Pressing, (Approach)e.newValue)));
            card.Add(approach);

            // The profile comes off the outcome, derived through the run's own tactics balance. Calling
            // ChanceProfile.FromTactics here read the untuned defaults, so an assigned SimulationBalanceSO
            // moved the sim and left this line quoting numbers the match never used.
            ChanceProfile profile = _lineup.ChanceProfile;
            int volume = Mathf.RoundToInt((float)(profile.Volume * 100f)) - 100;
            int quality = Mathf.RoundToInt((float)(profile.Quality * 100f)) - 100;
            card.Add(MakeLabel(
                "Chances vs balanced: " + Signed(volume) + "% shots · " + Signed(quality) + "% quality",
                10, HarnessPalette.Muted));
            card.Add(MakeLabel(ChanceSummary(volume, quality), 10, HarnessPalette.Accent));

            return card;
        }

        private static string ChanceSummary(int volume, int quality)
        {
            if (volume == 0 && quality == 0)
            {
                return "Balanced — an even mix of chances.";
            }

            string shots = volume > 0 ? "more shots" : volume < 0 ? "fewer shots" : "as many shots";
            string sharpness = quality > 0 ? "each one sharper" : quality < 0 ? "each one less clinical" : "same quality";
            return shots + ", " + sharpness + ".";
        }

        private VisualElement BuildSquadCard()
        {
            Squad squad = _session.Squad;
            VisualElement card = MakeCard();
            card.Add(MakeLabel(
                "YOUR SQUAD · " + _session.ManagedClubName.ToUpperInvariant() + "  ·  " +
                _lineup.Starters.Count + "/" + _lineup.Formation.Total + " STARTING",
                11, HarnessPalette.Muted, bold: true));

            // Straight off the outcome: the same derivation the match runs, through the run's trait catalog
            // and tactics balance. A locally-built EffectiveStrengthBuilder binds TraitCatalog.Default, so
            // these pills used to disagree with the season whenever a catalog asset was assigned.
            TeamStrength strength = _lineup.Strength;
            var axes = new VisualElement();
            axes.style.flexDirection = FlexDirection.Row;
            axes.style.marginTop = 6;
            axes.style.marginBottom = 4;
            axes.Add(MakeAxisPill("ATK", strength.Attack));
            axes.Add(MakeAxisPill("MID", strength.Midfield));
            axes.Add(MakeAxisPill("DEF", strength.Defence));
            card.Add(axes);

            if (squad != null)
            {
                AppendSquadLine(card, "GOALKEEPERS", squad, Position.Goalkeeper);
                AppendSquadLine(card, "DEFENDERS", squad, Position.Defender);
                AppendSquadLine(card, "MIDFIELDERS", squad, Position.Midfielder);
                AppendSquadLine(card, "FORWARDS", squad, Position.Forward);
            }

            return card;
        }

        private VisualElement MakeAxisPill(string label, double value)
        {
            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.marginRight = 14;
            pill.Add(MakeLabel(label + " ", 12, HarnessPalette.Muted, bold: true));
            pill.Add(MakeLabel(Mathf.RoundToInt((float)value).ToString(), 14, HarnessPalette.Accent, bold: true));
            return pill;
        }

        private void AppendSquadLine(VisualElement card, string heading, Squad squad, Position position)
        {
            Label lineHead = MakeLabel(heading, 10, HarnessPalette.Muted, bold: true);
            lineHead.style.marginTop = 8;
            lineHead.style.letterSpacing = 1f;
            card.Add(lineHead);

            bool canSell = _session.IsWindowOpen;
            foreach (Player player in squad.Players)
            {
                if (player.Position != position)
                {
                    continue;
                }

                bool starting = IsStarting(player.Id.Value);

                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;

                var playerId = new PlayerId(player.Id.Value);
                var toggle = new Button(() => Apply(_session.ToggleStarter(playerId))) { text = starting ? "★" : "·" };
                toggle.style.width = 22;
                toggle.style.height = 18;
                toggle.style.marginRight = 6;
                toggle.style.paddingLeft = 0;
                toggle.style.paddingRight = 0;
                toggle.style.backgroundColor = starting ? HarnessPalette.Accent : new Color(0, 0, 0, 0);
                toggle.style.color = starting ? HarnessPalette.Pitch : HarnessPalette.Muted;
                toggle.style.unityFontStyleAndWeight = FontStyle.Bold;
                SetRadius(toggle, 4);
                row.Add(toggle);

                var left = MakeLabel(player.Name + "  ·  " + HarnessLabels.RoleLabel(player.Role) + "  ·  " + player.Age, 11,
                    starting ? HarnessPalette.Chalk : HarnessPalette.Muted);
                left.style.flexGrow = 1;
                row.Add(left);

                // The drama layer, still live: a wound or a lift a decision left, for as long as it lasts.
                row.Add(HarnessMorale.MakeBadgeFor(_session.MoralePointsOf(player.Id)));

                var ovr = MakeLabel("OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)), 11, HarnessPalette.Accent, bold: true);
                ovr.style.marginRight = 8;
                row.Add(ovr);

                row.Add(TraitBadges(player));
                row.Add(BuildKeyStats(player));

                // Selling is only possible in an open window — the squad is otherwise locked for the season.
                if (canSell)
                {
                    Player target = player;
                    var sell = new Button(() => Sell(target)) { text = "Sell " + FormatValue(_session.FeeOf(player)) };
                    StyleActionButton(sell, HarnessPalette.Loss);
                    sell.style.marginLeft = 8;
                    row.Add(sell);
                }

                card.Add(row);
            }
        }

        private static VisualElement BuildKeyStats(Player player)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Row;

            foreach (AttributeKey key in RoleKeyAttributes.For(player.Role))
            {
                byte value = key.Read(player.Attributes);
                Label chip = MakeLabel(HarnessLabels.AttributeLabel(key) + " " + value, 10, AttributeColor(value), value >= 85);
                chip.style.marginLeft = 10;
                wrap.Add(chip);
            }

            return wrap;
        }

        // Small badge chips for the player's traits — the character layer made visible on the roster
        // (dev-tool labels from the id slug; the shipped UI will read localized names off the catalog).
        private static VisualElement TraitBadges(Player player)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Row;
            FillTraitBadges(wrap, player);
            return wrap;
        }

        // Split out so a recycled row can refill an existing strip after clearing it, rather than swapping
        // in a fresh container each bind.
        private static void FillTraitBadges(VisualElement wrap, Player player)
        {
            foreach (Gaffer.Domain.Traits.TraitId id in player.Traits)
            {
                Label badge = MakeLabel(id.Value.Replace('-', ' ').ToUpperInvariant(), 9, HarnessPalette.Draw, bold: true);
                badge.style.marginLeft = 8;
                badge.style.paddingLeft = 4;
                badge.style.paddingRight = 4;
                badge.style.borderLeftWidth = 1;
                badge.style.borderRightWidth = 1;
                badge.style.borderTopWidth = 1;
                badge.style.borderBottomWidth = 1;
                badge.style.borderLeftColor = HarnessPalette.Draw;
                badge.style.borderRightColor = HarnessPalette.Draw;
                badge.style.borderTopColor = HarnessPalette.Draw;
                badge.style.borderBottomColor = HarnessPalette.Draw;
                SetRadius(badge, 3);
                wrap.Add(badge);
            }
        }

        private static Color AttributeColor(byte value)
        {
            if (value >= 85)
            {
                return HarnessPalette.Accent;
            }

            if (value >= 70)
            {
                return HarnessPalette.Chalk;
            }

            if (value >= 55)
            {
                return new Color(0.616f, 0.667f, 0.643f);
            }

            if (value >= 40)
            {
                return HarnessPalette.Muted;
            }

            return new Color(0.275f, 0.337f, 0.310f);
        }

        // ----- Transfer market card ------------------------------------------------------------------------

        // The row draws a fixed four lines so the ListView can virtualize by height: it creates only as
        // many rows as fit the viewport and rebinds them as you scroll. Nothing scales with the market.
        private const float MarketRowHeight = 78f;
        private const float MarketListHeight = 420f;

        private VisualElement BuildTransferCard()
        {
            VisualElement card = MakeCard();

            SyncMarketOrder();
            FilterMarket();

            TransferWindowPhase phase = _session.WindowPhase;
            bool open = phase != TransferWindowPhase.Closed;
            string label = phase == TransferWindowPhase.Summer ? "SUMMER WINDOW · OPEN"
                : phase == TransferWindowPhase.Winter ? "WINTER WINDOW · OPEN"
                : "TRANSFER WINDOW · CLOSED";
            Color labelColor = open ? HarnessPalette.Win : HarnessPalette.Muted;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.justifyContent = Justify.SpaceBetween;
            _marketHeading = MakeLabel(MarketHeading(), 11, HarnessPalette.Muted, bold: true);
            head.Add(_marketHeading);
            head.Add(MakeLabel(label, 11, labelColor, bold: true));
            card.Add(head);

            _marketMask = MakeLabel(
                open ? MaskNote() : "The market is closed. It opens in the summer (pre-season) and at the winter break.",
                10, HarnessPalette.Muted);
            card.Add(_marketMask);

            if (!string.IsNullOrEmpty(_transferStatus))
            {
                card.Add(MakeLabel(_transferStatus, 11, HarnessPalette.Chalk));
            }

            if (!open)
            {
                return card;
            }

            card.Add(MakeLabel(
                "The fee and the weekly wage are both on the button: a signing has to clear the cash AND the wage room.",
                10, HarnessPalette.Muted));

            // Filter the shortlist by specific role (left back, right back, …) and by an age range.
            var roleChoices = new List<string> { "All positions" };
            foreach (PlayerRole role in FilterRoles)
            {
                roleChoices.Add(RoleName(role));
            }

            // The filters narrow the source list, not the hierarchy: they refill _marketShown and refresh
            // the ListView. Refresh() would have rebuilt the whole window on every keystroke in an age
            // field.
            var roleFilter = new DropdownField("Position", roleChoices, RoleFilterIndex());
            roleFilter.RegisterValueChangedCallback(e =>
            {
                int index = roleChoices.IndexOf(e.newValue);
                _marketRoleFilter = index <= 0 ? (PlayerRole?)null : FilterRoles[index - 1];
                RefilterMarket();
            });
            card.Add(roleFilter);

            var ageRow = new VisualElement();
            ageRow.style.flexDirection = FlexDirection.Row;
            var minAge = new IntegerField("Min age") { value = _marketMinAge };
            minAge.style.flexGrow = 1;
            minAge.RegisterValueChangedCallback(e =>
            {
                _marketMinAge = e.newValue;
                RefilterMarket();
            });
            ageRow.Add(minAge);
            var maxAge = new IntegerField("Max age") { value = _marketMaxAge };
            maxAge.style.flexGrow = 1;
            maxAge.style.marginLeft = 8;
            maxAge.RegisterValueChangedCallback(e =>
            {
                _marketMaxAge = e.newValue;
                RefilterMarket();
            });
            ageRow.Add(maxAge);
            card.Add(ageRow);

            // Virtualized. The card used to build five to eight VisualElements per prospect for the whole
            // shortlist and scout every one of them on the way; the ListView holds about five live rows
            // regardless of the pool, and only those get Observe'd (bindItem).
            _marketList = new ListView
            {
                fixedItemHeight = MarketRowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                showBorder = false,
                showAlternatingRowBackgrounds = AlternatingRowBackground.None,
                reorderable = false,
                horizontalScrollingEnabled = false,
                makeItem = MakeMarketRow,
                bindItem = BindMarketRow,
                unbindItem = UnbindMarketRow,
                itemsSource = _marketShown,
            };
            _marketList.style.height = MarketListHeight;
            _marketList.style.marginTop = 6;
            card.Add(_marketList);

            _marketEmpty = MakeLabel("No prospects match this filter.", 10, HarnessPalette.Muted);
            card.Add(_marketEmpty);
            ShowMarketState();

            return card;
        }

        // ----- The market list's source and repaints -------------------------------------------------------

        private string MarketHeading()
        {
            return _marketShown.Count == _marketOrder.Count
                ? "TRANSFER MARKET — " + _marketOrder.Count + " PROSPECTS"
                : "TRANSFER MARKET — " + _marketShown.Count + " OF " + _marketOrder.Count + " PROSPECTS";
        }

        private string MaskNote()
        {
            return _reveal
                ? "Revealing true potential (dev)."
                : "OVR is current ability; potential is scout-masked — trust the band, take the punt.";
        }

        // Refills the strongest-first order, but only when the run's market has actually moved under it.
        // _session.GetMarket() is the session's own list: a signing, a sale or a drama sale mutates it in place
        // (so the count moves) and a summer rollover replaces it outright (so the instance moves). Nothing
        // else touches it — market players do not develop mid-season — so instance-plus-count is a
        // sufficient watch, and it keeps an n log n sort off every week advance and every drag-drop.
        private void SyncMarketOrder()
        {
            IReadOnlyList<Player> market = _session.GetMarket();
            if (ReferenceEquals(market, _orderedFrom) && market.Count == _orderedCount)
            {
                return;
            }

            ByOverallDescending(market, _marketOrder);
            _orderedFrom = market;
            _orderedCount = market.Count;
        }

        // The filters, applied to the ordered pool. A plain scan — the only work here that is still
        // proportional to the market, and it is two comparisons per player, not a rating and a row.
        private void FilterMarket()
        {
            _marketShown.Clear();
            for (int i = 0; i < _marketOrder.Count; i++)
            {
                Player player = _marketOrder[i];
                if (_marketRoleFilter != null && player.Role != _marketRoleFilter.Value)
                {
                    continue;
                }

                if (player.Age < _marketMinAge || player.Age > _marketMaxAge)
                {
                    continue;
                }

                _marketShown.Add(player);
            }
        }

        private void RefilterMarket()
        {
            if (_marketList == null)
            {
                return;
            }

            FilterMarket();
            _marketList.RefreshItems();
            ShowMarketState();
        }

        // Repaints exactly what a scouting-accuracy or reveal change alters: the visible rows and the line
        // of copy that quotes the setting. Nothing is created or destroyed, so the accuracy slider can
        // afford to run this on every frame of a drag. No-ops before the first Refresh with a session.
        private void RebindMarket()
        {
            if (_marketMask != null && _session != null && _session.IsWindowOpen)
            {
                _marketMask.text = MaskNote();
            }

            if (_marketList != null)
            {
                _marketList.RefreshItems();
            }
        }

        private void ShowMarketState()
        {
            if (_marketHeading != null)
            {
                _marketHeading.text = MarketHeading();
            }

            bool empty = _marketShown.Count == 0;
            if (_marketList != null)
            {
                _marketList.style.display = empty ? DisplayStyle.None : DisplayStyle.Flex;
            }

            if (_marketEmpty != null)
            {
                _marketEmpty.style.display = empty ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private VisualElement MakeMarketRow()
        {
            return new MarketRowView(this).Root;
        }

        // The only per-prospect work left, and it runs for visible rows only: one scout report and one
        // affordability read, both for a player who is actually on screen.
        private void BindMarketRow(VisualElement element, int index)
        {
            var view = (MarketRowView)element.userData;
            if (index < 0 || index >= _marketShown.Count)
            {
                view.Unbind();
                return;
            }

            Player player = _marketShown[index];
            view.Bind(
                player,
                _session.Observe(player, _accuracy),
                SigningVerdict.For(_session.FeeOf(player), _session.WeeklyWageOf(player), _session.Finances),
                _session.ValueOf(player),
                _session.MoralePointsOf(player.Id),
                _reveal);
        }

        private static void UnbindMarketRow(VisualElement element, int index)
        {
            ((MarketRowView)element.userData).Unbind();
        }

        /// <summary>
        /// One recycled market row. The ListView creates a handful of these — as many as fit the viewport —
        /// and rebinds them as you scroll, so every row object shows many different players over its life.
        ///
        /// <para><b>The sign button's handler is registered once, here, and never per bind.</b> It
        /// dispatches through <see cref="_player"/>, the single field <see cref="Bind"/> overwrites and
        /// <see cref="Unbind"/> clears. That is deliberate: a handler added in <c>bindItem</c> would have to
        /// be removed in <c>unbindItem</c> or it would pile up on the recycled row and eventually sign a
        /// player who left the screen long ago — the classic failure of this pattern (PERFORMANCE.md §4,
        /// UNITY.md §5). With one handler for the row's whole life there is nothing to unregister, and the
        /// worst a stale click can do is nothing at all, because an unbound row has no player.</para>
        /// </summary>
        private sealed class MarketRowView
        {
            private readonly ManagementWindow _window;
            private readonly Label _name;
            private readonly Label _morale;
            private readonly VisualElement _badges;
            private readonly Label _rating;
            private readonly Label _worth;
            private readonly Button _sign;
            private readonly Label _books;
            private readonly Label _potential;
            private readonly Label _attributes;

            // Who this row is showing right now. The row's only piece of per-bind state, and the only
            // thing the button reads.
            private Player _player;

            internal MarketRowView(ManagementWindow window)
            {
                _window = window;

                Root = new VisualElement();
                Root.style.height = MarketRowHeight;
                Root.style.paddingTop = 6;
                Root.style.paddingBottom = 6;
                Root.style.paddingLeft = 6;
                Root.style.borderBottomWidth = 1;
                Root.style.borderBottomColor = HarnessPalette.PitchLine;
                Root.style.borderLeftColor = HarnessPalette.Loss;
                Root.style.overflow = Overflow.Hidden;
                Root.userData = this;

                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;

                _name = MakeLabel(string.Empty, 12, HarnessPalette.Chalk, bold: true);
                _name.style.flexGrow = 1;
                _name.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(_name);

                // A man drama forced out lands back here still carrying his wound, so the chip belongs on
                // the market row too. Created once with the row; Bind and Unbind both write it.
                _morale = HarnessMorale.MakeBadge();
                line.Add(_morale);

                _badges = new VisualElement();
                _badges.style.flexDirection = FlexDirection.Row;
                line.Add(_badges);

                _rating = MakeLabel(string.Empty, 12, HarnessPalette.Accent, bold: true);
                _rating.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(_rating);

                _worth = MakeLabel(string.Empty, 11, HarnessPalette.Muted);
                _worth.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(_worth);

                _sign = new Button(SignCurrent);
                StyleActionButton(_sign, HarnessPalette.Accent);
                line.Add(_sign);
                Root.Add(line);

                _books = MakeLabel(string.Empty, 10, HarnessPalette.Muted);
                _books.style.whiteSpace = WhiteSpace.NoWrap;
                Root.Add(_books);

                _potential = MakeLabel(string.Empty, 11, HarnessPalette.Accent);
                _potential.style.whiteSpace = WhiteSpace.NoWrap;
                Root.Add(_potential);

                _attributes = MakeLabel(string.Empty, 10, HarnessPalette.Muted);
                _attributes.style.whiteSpace = WhiteSpace.NoWrap;
                Root.Add(_attributes);
            }

            internal VisualElement Root { get; }

            /// <summary>
            /// Repaints the row for a different player. Every mutable thing the row draws — including the
            /// variable-length trait strip, which is cleared before it is refilled, and the player the
            /// button will act on — is assigned here unconditionally: no "only if it changed" branch, no
            /// field left holding the previous occupant. A rebind that forgets one of these is how a
            /// recycled row signs the wrong man, so the reset is total rather than incremental.
            /// </summary>
            internal void Bind(Player player, ScoutReport report, SigningVerdict verdict, long worth, double moralePoints, bool reveal)
            {
                _player = player;

                _name.text = player.Name + "  ·  " + HarnessLabels.RoleLabel(player.Role) + "  ·  " + player.Age;

                // Written on every bind, zero or not, so a recycled row cannot keep the last man's morale.
                HarnessMorale.Show(_morale, moralePoints);

                _badges.Clear();
                FillTraitBadges(_badges, player);

                _rating.text = "OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)) + "   ";
                _worth.text = FormatValue(worth) + " worth   ";

                _sign.text = verdict.ActionLabel();
                _sign.SetEnabled(true);
                _sign.style.backgroundColor = verdict.Affordable ? HarnessPalette.Accent : HarnessPalette.PitchLine;
                _sign.style.color = verdict.Affordable ? HarnessPalette.Pitch : HarnessPalette.Muted;

                // The blocked reason is shown, not enforced: the button stays live so the session still
                // gets the click and TransferService still writes the authoritative message into
                // _transferStatus. The row only says in advance what that message would be.
                _books.text = verdict.Sentence();
                _books.style.color = verdict.Tone;
                Root.style.borderLeftWidth = verdict.Affordable ? 0 : 2;

                _potential.text = reveal
                    ? "Potential " + report.PotentialLow + "–" + report.PotentialHigh + "   (true " + player.HiddenPotential + ")"
                    : "Potential " + report.PotentialLow + "–" + report.PotentialHigh;
                _attributes.text = FormatScoutAttributes(report);
            }

            /// <summary>
            /// Lets go of the player. Called when the ListView recycles the row out of view, so a row
            /// between occupants shows nothing and — because the button reads <see cref="_player"/> — can
            /// sign nobody.
            /// </summary>
            internal void Unbind()
            {
                _player = null;
                _name.text = string.Empty;
                HarnessMorale.Show(_morale, 0.0);
                _badges.Clear();
                _rating.text = string.Empty;
                _worth.text = string.Empty;
                _sign.text = string.Empty;
                _sign.SetEnabled(false);
                _books.text = string.Empty;
                _potential.text = string.Empty;
                _attributes.text = string.Empty;
                Root.style.borderLeftWidth = 0;
            }

            private void SignCurrent()
            {
                if (_player != null)
                {
                    _window.Sign(_player);
                }
            }
        }

        // The specific roles offered in the market filter, top to bottom of the pitch.
        private static readonly PlayerRole[] FilterRoles =
        {
            PlayerRole.Goalkeeper, PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
            PlayerRole.DefensiveMidfield, PlayerRole.CentralMidfield, PlayerRole.AttackingMidfield,
            PlayerRole.RightMidfield, PlayerRole.LeftMidfield, PlayerRole.RightWing, PlayerRole.LeftWing,
            PlayerRole.Striker,
        };

        private int RoleFilterIndex()
        {
            if (_marketRoleFilter == null)
            {
                return 0;
            }

            for (int i = 0; i < FilterRoles.Length; i++)
            {
                if (FilterRoles[i] == _marketRoleFilter.Value)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static string RoleName(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper: return "Goalkeeper";
                case PlayerRole.RightBack: return "Right Back";
                case PlayerRole.CentreBack: return "Centre Back";
                case PlayerRole.LeftBack: return "Left Back";
                case PlayerRole.DefensiveMidfield: return "Defensive Midfield";
                case PlayerRole.CentralMidfield: return "Central Midfield";
                case PlayerRole.AttackingMidfield: return "Attacking Midfield";
                case PlayerRole.RightMidfield: return "Right Midfield";
                case PlayerRole.LeftMidfield: return "Left Midfield";
                case PlayerRole.RightWing: return "Right Wing";
                case PlayerRole.LeftWing: return "Left Wing";
                case PlayerRole.Striker: return "Striker";
                default: return HarnessLabels.RoleLabel(role);
            }
        }

        // Strongest first, ties broken on the lower player id so the order is stable across renders.
        //
        // Each player is rated exactly once, into an array, and the sort compares array entries. The
        // comparator used to call PlayerRatings.ForRole twice per comparison, which is ~2·n·log n rating
        // evaluations — about 1.5 million for a 50,000-player market, for 50,000 players' worth of data.
        private static void ByOverallDescending(IReadOnlyList<Player> players, List<Player> into)
        {
            int count = players.Count;
            into.Clear();
            if (count == 0)
            {
                return;
            }

            var ratings = new double[count];
            var ids = new int[count];
            var order = new int[count];
            for (int i = 0; i < count; i++)
            {
                Player player = players[i];
                ratings[i] = PlayerRatings.ForRole(player);
                ids[i] = player.Id.Value;
                order[i] = i;
            }

            Array.Sort(order, (a, b) =>
            {
                int byRating = ratings[b].CompareTo(ratings[a]);
                return byRating != 0 ? byRating : ids[a].CompareTo(ids[b]);
            });

            for (int i = 0; i < count; i++)
            {
                into.Add(players[order[i]]);
            }
        }

        private static string FormatScoutAttributes(ScoutReport report)
        {
            var parts = new List<string>(report.KeyAttributes.Count);
            foreach (AttributeEstimate estimate in report.KeyAttributes)
            {
                string band = estimate.Low == estimate.High
                    ? estimate.Low.ToString()
                    : estimate.Low + "–" + estimate.High;
                parts.Add(HarnessLabels.LabelForKey(estimate.LabelKey) + " " + band);
            }

            return string.Join("   ", parts);
        }

        private static void StyleActionButton(Button button, Color color)
        {
            button.style.backgroundColor = color;
            button.style.color = HarnessPalette.Pitch;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.height = 20;
            button.style.fontSize = 10;
            SetRadius(button, 4);
        }

        // ----- Results + table -----------------------------------------------------------------------------

        private VisualElement BuildLastWeekCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("LAST WEEK · ROUND " + (_lastWeek.Round + 1), 11, HarnessPalette.Muted, bold: true));

            ClubId managed = _session.ManagedClub;
            foreach (MatchResult match in _lastWeek.Matches)
            {
                bool involvesManaged = match.Home == managed || match.Away == managed;
                Color scoreColor = involvesManaged ? HarnessPalette.Accent : HarnessPalette.Chalk;

                var block = new VisualElement();
                block.style.marginTop = 6;

                block.Add(MakeLabel(
                    _session.ClubName(match.Home) + "  " + match.HomeGoals + " - " + match.AwayGoals + "  " + _session.ClubName(match.Away),
                    12, scoreColor, involvesManaged));

                if (match.HomeShots + match.AwayShots > 0)
                {
                    block.Add(MakeLabel("shots " + match.HomeShots + "–" + match.AwayShots, 10, HarnessPalette.Muted));
                }

                string scorers = FormatScorers(match);
                if (scorers.Length > 0)
                {
                    block.Add(MakeLabel(scorers, 10, HarnessPalette.Muted));
                }

                card.Add(block);
            }

            return card;
        }

        private string FormatScorers(MatchResult match)
        {
            var home = new List<string>();
            var away = new List<string>();
            foreach (MatchEvent matchEvent in match.Events)
            {
                if (matchEvent.Kind != MatchEventKind.Goal)
                {
                    continue;
                }

                if (matchEvent.Side == TeamSide.Home)
                {
                    home.Add(ScorerEntry(match.Home, matchEvent));
                }
                else
                {
                    away.Add(ScorerEntry(match.Away, matchEvent));
                }
            }

            if (home.Count == 0 && away.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            if (home.Count > 0)
            {
                parts.Add(_session.ClubName(match.Home) + "  " + string.Join(", ", home));
            }

            if (away.Count > 0)
            {
                parts.Add(_session.ClubName(match.Away) + "  " + string.Join(", ", away));
            }

            return string.Join("      ", parts);
        }

        private string ScorerEntry(ClubId club, MatchEvent goal)
        {
            string name = ScorerSurname(club, goal.Scorer);
            return name.Length > 0 ? name + " " + goal.Minute + "'" : goal.Minute + "'";
        }

        // The session knows where to look for a name — the managed club through the live season (it may hold
        // a just-signed scorer), every other club through the league. That branch used to be copied here.
        private string ScorerSurname(ClubId club, PlayerId? scorer)
        {
            if (scorer == null)
            {
                return string.Empty;
            }

            string name = _session.PlayerName(club, scorer.Value);
            return name.Length > 0 ? Surname(name) : string.Empty;
        }

        private VisualElement BuildVerdictBanner()
        {
            SeasonVerdict? verdict = _session.Verdict;
            Color color = verdict == SeasonVerdict.Promoted ? HarnessPalette.Win
                : verdict == SeasonVerdict.Sacked ? HarnessPalette.Loss : HarnessPalette.Chalk;
            string headline = verdict == SeasonVerdict.Promoted ? "PROMOTED"
                : verdict == SeasonVerdict.Sacked ? "SACKED" : "RETAINED";

            VisualElement banner = MakeCard();
            banner.style.borderLeftWidth = 4;
            banner.style.borderLeftColor = color;
            banner.style.marginTop = 8;

            // The final standing is on the week that ended the season, not re-derived from the table here.
            // A season finished before this window ever saw a week (a save loaded at full time) has no such
            // outcome to replay, so it falls back to the session's own render-time query.
            int position = _lastWeek != null && _lastWeek.FinalPosition > 0 ? _lastWeek.FinalPosition : _session.TablePosition;
            banner.Add(MakeLabel(headline, 24, color, bold: true));
            banner.Add(MakeLabel(_session.ManagedClubName + " finished " + Ordinal(position) + ".", 12, HarnessPalette.Muted));
            return banner;
        }

        private VisualElement BuildTableCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("LEAGUE TABLE", 11, HarnessPalette.Muted, bold: true));

            string[] heads = { "#", "Club", "P", "W", "D", "L", "GF", "GA", "GD", "Pts" };
            card.Add(MakeRow(heads, HarnessPalette.Muted, bold: true, background: new Color(0, 0, 0, 0)));

            ClubId managed = _session.ManagedClub;
            BoardTarget target = _session.BoardTarget;
            IReadOnlyList<LeagueTableRow> table = _session.Standings();
            for (int i = 0; i < table.Count; i++)
            {
                LeagueTableRow row = table[i];
                int position = i + 1;
                bool isManaged = row.Club == managed;

                Color background = isManaged ? Tint(HarnessPalette.Accent, 0.16f)
                    : position <= target.PromotionPosition ? Tint(HarnessPalette.Win, 0.07f)
                    : position > target.SurvivalPosition ? Tint(HarnessPalette.Loss, 0.07f)
                    : new Color(0, 0, 0, 0);

                string[] cells =
                {
                    position.ToString(), _session.ClubName(row.Club), row.Played.ToString(),
                    row.Won.ToString(), row.Drawn.ToString(), row.Lost.ToString(), row.GoalsFor.ToString(),
                    row.GoalsAgainst.ToString(), Signed(row.GoalDifference), row.Points.ToString(),
                };
                VisualElement rowElement = MakeRow(cells, HarnessPalette.Chalk, bold: false, background: background);
                if (isManaged)
                {
                    rowElement.style.borderLeftWidth = 3;
                    rowElement.style.borderLeftColor = HarnessPalette.Accent;
                }

                card.Add(rowElement);
            }

            return card;
        }

        private VisualElement MakeRow(IReadOnlyList<string> cells, Color color, bool bold, Color background)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.backgroundColor = background;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = HarnessPalette.PitchLine;

            float[] widths = { 26, -1, 26, 26, 26, 26, 30, 30, 32, 34 };
            for (int i = 0; i < cells.Count; i++)
            {
                var cell = new Label(cells[i]);
                cell.style.fontSize = 11;
                cell.style.color = color;
                if (bold || i == 9)
                {
                    cell.style.unityFontStyleAndWeight = FontStyle.Bold;
                }

                if (widths[i] < 0)
                {
                    cell.style.flexGrow = 1;
                    cell.style.unityTextAlign = TextAnchor.MiddleLeft;
                }
                else
                {
                    cell.style.width = widths[i];
                    cell.style.flexShrink = 0;
                    cell.style.unityTextAlign = i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight;
                }

                row.Add(cell);
            }

            return row;
        }

        // ----- Shared UI helpers ---------------------------------------------------------------------------

        // One implementation, shared with the affordability copy, so a fee on a button and the shortfall
        // under it can never be written two different ways.
        private static string FormatValue(long value)
        {
            return HarnessMoney.Format(value);
        }

        private static Label MakeLabel(string text, int size, Color color, bool bold = false)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            if (bold)
            {
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
            }

            return label;
        }

        private static VisualElement MakeCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = HarnessPalette.PitchRaised;
            SetBorder(card, HarnessPalette.PitchLine, 1);
            SetRadius(card, 10);
            SetPadding(card, 14);
            card.style.marginTop = 8;
            return card;
        }

        private static void SetBorder(VisualElement element, Color color, float width)
        {
            element.style.borderTopWidth = width;
            element.style.borderBottomWidth = width;
            element.style.borderLeftWidth = width;
            element.style.borderRightWidth = width;
            element.style.borderTopColor = color;
            element.style.borderBottomColor = color;
            element.style.borderLeftColor = color;
            element.style.borderRightColor = color;
        }

        private static void SetRadius(VisualElement element, float radius)
        {
            element.style.borderTopLeftRadius = radius;
            element.style.borderTopRightRadius = radius;
            element.style.borderBottomLeftRadius = radius;
            element.style.borderBottomRightRadius = radius;
        }

        private static void SetPadding(VisualElement element, float padding)
        {
            element.style.paddingTop = padding;
            element.style.paddingBottom = padding;
            element.style.paddingLeft = padding;
            element.style.paddingRight = padding;
        }

        private static Color Tint(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static string Signed(int value)
        {
            return value > 0 ? "+" + value : value.ToString();
        }

        private static string Ordinal(int position)
        {
            return position + ".";
        }
    }
}
