using System.Collections.Generic;
using System.IO;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Editor.Balance;
using Gaffer.Editor.Harness;
using Gaffer.Infrastructure.Configuration;
using Gaffer.Infrastructure.Persistence;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Position = Gaffer.Domain.Players.Position;

namespace Gaffer.Editor.Management
{
    /// <summary>
    /// The unified management bench: play a club through a league season week by week AND run its transfers in
    /// the same window — Season Player and Transfer Market fused. This is not a UI merge alone, it is the
    /// economy wired into the season loop: wages drain the transfer cash every week (GDD §4.4, no longer a
    /// passive cap), a signing or sale updates the live squad, strength, and eleven the same week
    /// (LeagueSeason.UpdateSquad), and the market is only open in the summer and winter windows
    /// (TransferWindow). Advance a season to age + develop + renew every squad. Styled in the ART_STYLE
    /// broadcast identity. Not shipped; a preview of the run the real UI (Faz 7) will present.
    /// </summary>
    public sealed class ManagementWindow : EditorWindow
    {
        private const int MaxTeams = 64;
        private const int MarketIdBase = 1_000_000;

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

        private Tactics _tactics = Tactics.Balanced;
        private Formation _formation = Formation.F442;
        private Player[] _lineup;
        private int _dragFromSlot = -1;
        private int _dragFromBenchId = -1;
        private string _lineupStatus;
        private VisualElement _dragGhost;
        private readonly List<VisualElement> _slotTokens = new List<VisualElement>();

        private League _league;
        private LeagueSeason _season;
        private ClubId _managedClub;
        private BoardTarget _target;
        private MatchSimulator _simulator;
        private MatchContext _context;
        private SeasonVerdict? _verdict;
        private WeekResult _lastWeek;
        private int _seasonNumber = 1;
        private List<Player> _retired;
        private List<Player> _arrived;

        private Finances _finances;
        private List<Player> _market;
        private readonly Scout _scout = new Scout();
        private string _transferStatus;
        private PlayerRole? _marketRoleFilter;
        private int _marketMinAge = 15;
        private int _marketMaxAge = 40;

        private string _saveStatus;
        private SimulationBalanceSO _simulationBalance;
        private DevelopmentBalanceSO _developmentBalance;
        private RenewalBalanceSO _renewalBalance;

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

            var accuracy = new Slider("Scout accuracy", 0f, 1f) { value = _accuracy };
            accuracy.RegisterValueChangedCallback(e =>
            {
                _accuracy = e.newValue;
                if (_season != null)
                {
                    Refresh();
                }
            });
            card.Add(accuracy);

            var reveal = new Toggle("Reveal true potential (dev)") { value = _reveal };
            reveal.RegisterValueChangedCallback(e =>
            {
                _reveal = e.newValue;
                if (_season != null)
                {
                    Refresh();
                }
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

        private MatchSimulationSettings SimSettings()
        {
            return _simulationBalance != null ? _simulationBalance.ToSettings() : MatchSimulationSettings.Default;
        }

        private DevelopmentSettings DevSettings()
        {
            return _developmentBalance != null ? _developmentBalance.ToSettings() : DevelopmentSettings.Default;
        }

        private RenewalSettings RenewSettings()
        {
            return _renewalBalance != null ? _renewalBalance.ToSettings() : RenewalSettings.Default;
        }

        private void StartSeason()
        {
            int count = Mathf.Clamp(_teamCount, 4, MaxTeams);
            _league = BuildLeague(count);
            _season = new LeagueSeason(_league);
            _simulator = new MatchSimulator(
                new PoissonChanceGenerator(SimSettings()),
                new QualityChanceResolver());
            _context = new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false);
            _managedClub = new ClubId(Mathf.Clamp(_managedIndex, 0, count - 1));
            _target = new BoardTarget(_promotionPosition, _survivalPosition);
            _seasonNumber = 1;
            _retired = null;
            _arrived = null;

            _finances = new Finances(_startingCash, _wageBudget, TotalWages(ManagedSquad()));
            _market = GenerateMarket();
            _transferStatus = null;

            AutoPickStarters();
            _season.SetFormation(_managedClub, _formation);
            _season.SetStarters(_managedClub, CurrentStarters());
            _season.SetTactics(_managedClub, _tactics);
            _verdict = null;
            _lastWeek = null;

            Refresh();
        }

        // A free-agent market to scout and sign from — a pool with a few guaranteed gems (TDD §5). Both the
        // generation seed and the id range are shifted by the season number, so each season shows a genuinely
        // fresh set of prospects (not the same names again) and a player signed from an earlier season's market
        // can never share an id with a current one. The base offset keeps every market id clear of the league's.
        private List<Player> GenerateMarket()
        {
            var gem = new GenerationContext
            {
                MinAge = 16, MaxAge = 19, MinAbility = 35, MaxAbility = 52, MinPotential = 84, MaxPotential = 95,
            };
            IReadOnlyList<Player> pool = new PlayerPoolGenerator(new PlayerGenerator()).GeneratePool(
                Mathf.Max(1, _marketSize), Mathf.Max(0, _gems), new GenerationContext(), gem,
                new SplitMix64RandomNumberGenerator(((ulong)_seed ^ 0xA5A5A5UL) + (ulong)_seasonNumber * 0x9E3779B97F4A7C15UL));

            int idBase = MarketIdBase + (_seasonNumber * 100_000);
            var market = new List<Player>(pool.Count);
            foreach (Player p in pool)
            {
                market.Add(WithId(p, idBase + p.Id.Value));
            }

            return market;
        }

        private static Player WithId(Player p, int id)
        {
            return new Player(new PlayerId(id), p.Name, p.Nationality, p.Role, p.Age, p.Attributes, p.HiddenPotential);
        }

        private static long TotalWages(Squad squad)
        {
            long total = 0;
            if (squad != null)
            {
                foreach (Player player in squad.Players)
                {
                    total += PlayerWage.Weekly(player);
                }
            }

            return total;
        }

        // Rolls the whole league on a year (SeasonTransition ages + develops + renews every squad), then starts
        // a fresh season with the same clubs. The managed squad's live signings are folded back into the league
        // first, so they age and develop too. Cash carries over; the wage bill is re-derived from the developed
        // squad and the budget is kept. A new window opens (summer), so the market can be worked again.
        private void StartNextSeason()
        {
            SyncLeague();
            IReadOnlyList<Player> before = ManagedSquad().Players;

            _seasonNumber++;
            _league = new SeasonTransition(DevSettings(), RenewSettings()).ToNextSeason(_league, (ulong)_seed, _seasonNumber);
            _season = new LeagueSeason(_league);
            ComputeSummer(before, ManagedSquad().Players);

            _finances = new Finances(_finances.Cash, _wageBudget, TotalWages(ManagedSquad()));
            _market = GenerateMarket();
            _transferStatus = null;

            AutoPickStarters();
            _season.SetFormation(_managedClub, _formation);
            _season.SetStarters(_managedClub, CurrentStarters());
            _season.SetTactics(_managedClub, _tactics);
            _verdict = null;
            _lastWeek = null;

            Refresh();
        }

        // Folds the managed club's live roster (after any signings/sales) back into the league, re-deriving its
        // strength, so a capture-to-save or a season transition sees the squad you actually built.
        private void SyncLeague()
        {
            Squad live = _season.SquadOf(_managedClub);
            if (live == null)
            {
                return;
            }

            var clubs = new List<Club>(_league.Clubs);
            Club old = clubs[_managedClub.Value];
            clubs[_managedClub.Value] = new Club(old.Id, old.Name, live, new EffectiveStrengthBuilder().Build(live));
            _league = new League(_league.Name, clubs);
        }

        private void SaveRun()
        {
            if (_season == null)
            {
                _saveStatus = "Start a season before saving.";
                Refresh();
                return;
            }

            SyncLeague();
            SeasonSaveData data = new SeasonSaveMapper().Capture(_league, _season, (ulong)_seed, _seasonNumber);
            Result result = SaveStore().Save(SavePath, data);
            _saveStatus = result.IsSuccess ? "Saved run to " + SavePath : result.Error;
            Refresh();
        }

        // Reads the run back and rebuilds the league, resumed season, and season number. Finances and the market
        // are not persisted yet (decision #18 — economy persistence deferred), so they are re-seeded from the
        // setup here; the run state (rosters, table) is what survives a reload.
        private void LoadRun()
        {
            Result<SeasonSaveData> loaded = SaveStore().Load(SavePath);
            if (loaded.IsFailure)
            {
                _saveStatus = loaded.Error;
                Refresh();
                return;
            }

            RestoredSeason restored = new SeasonSaveMapper().Restore(loaded.Value);
            _league = restored.League;
            _season = restored.Season;
            _seasonNumber = restored.SeasonNumber < 1 ? 1 : restored.SeasonNumber;

            EnsureRuntime();
            _managedClub = new ClubId(Mathf.Clamp(_managedIndex, 0, _league.Clubs.Count - 1));

            _finances = new Finances(_startingCash, _wageBudget, TotalWages(ManagedSquad()));
            _market = GenerateMarket();
            _transferStatus = null;

            AutoPickStarters();
            _season.SetFormation(_managedClub, _formation);
            _season.SetStarters(_managedClub, CurrentStarters());
            _season.SetTactics(_managedClub, _tactics);
            _verdict = null;
            _lastWeek = null;
            _retired = null;
            _arrived = null;
            CheckComplete();
            _saveStatus = "Loaded run from " + SavePath;
            Refresh();
        }

        private static JsonSaveStore SaveStore()
        {
            return new JsonSaveStore(new NewtonsoftJsonSerializer(), new SaveMigrator());
        }

        private void EnsureRuntime()
        {
            _simulator = new MatchSimulator(
                new PoissonChanceGenerator(SimSettings()),
                new QualityChanceResolver());
            _context = new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false);
            _target = new BoardTarget(_promotionPosition, _survivalPosition);
        }

        private void ComputeSummer(IReadOnlyList<Player> before, IReadOnlyList<Player> after)
        {
            var afterIds = new HashSet<int>();
            foreach (Player p in after)
            {
                afterIds.Add(p.Id.Value);
            }

            var beforeIds = new HashSet<int>();
            foreach (Player p in before)
            {
                beforeIds.Add(p.Id.Value);
            }

            _retired = new List<Player>();
            foreach (Player p in before)
            {
                if (!afterIds.Contains(p.Id.Value))
                {
                    _retired.Add(p);
                }
            }

            _arrived = new List<Player>();
            foreach (Player p in after)
            {
                if (!beforeIds.Contains(p.Id.Value))
                {
                    _arrived.Add(p);
                }
            }
        }

        // The live managed roster — read through the season, not the league, so a mid-season signing or sale is
        // reflected everywhere at once.
        private Squad ManagedSquad()
        {
            return _season.SquadOf(_managedClub);
        }

        // ----- Transfers -----------------------------------------------------------------------------------

        private bool WindowOpen()
        {
            return _season != null && TransferWindow.IsOpen(_season.CurrentRound, _season.RoundCount);
        }

        private TransferWindowPhase WindowPhase()
        {
            return _season == null
                ? TransferWindowPhase.Closed
                : TransferWindow.At(_season.CurrentRound, _season.RoundCount);
        }

        private void Sign(Player player)
        {
            if (!WindowOpen())
            {
                _transferStatus = "The transfer window is closed.";
                Refresh();
                return;
            }

            Result<TransferResult> result = TransferService.Sign(_finances, ManagedSquad(), player);
            if (result.IsFailure)
            {
                _transferStatus = result.Error;
                Refresh();
                return;
            }

            _finances = result.Value.Finances;
            _season.UpdateSquad(_managedClub, result.Value.Squad);
            _market.Remove(player);
            SyncLeague();
            AutoPickStarters();
            _season.SetStarters(_managedClub, CurrentStarters());
            _transferStatus = "Signed " + player.Name + " for " + FormatValue(result.Value.Fee) +
                " (" + FormatValue(PlayerWage.Weekly(player)) + "/wk).";
            Refresh();
        }

        private void Sell(Player player)
        {
            if (!WindowOpen())
            {
                _transferStatus = "The transfer window is closed.";
                Refresh();
                return;
            }

            Result<TransferResult> result = TransferService.Sell(_finances, ManagedSquad(), player);
            if (result.IsFailure)
            {
                _transferStatus = result.Error;
                Refresh();
                return;
            }

            _finances = result.Value.Finances;
            _season.UpdateSquad(_managedClub, result.Value.Squad);
            _market.Add(player);
            SyncLeague();
            AutoPickStarters();
            _season.SetStarters(_managedClub, CurrentStarters());
            _transferStatus = "Sold " + player.Name + " for " + FormatValue(result.Value.Fee) + ".";
            Refresh();
        }

        // ----- Lineup --------------------------------------------------------------------------------------

        private void AutoPickStarters()
        {
            IReadOnlyList<Player> eleven = new LineupSelector().SelectBest(ManagedSquad(), _formation);
            _lineup = new Player[_formation.Total];
            for (int i = 0; i < eleven.Count && i < _lineup.Length; i++)
            {
                _lineup[i] = eleven[i];
            }
        }

        private IReadOnlyList<Player> CurrentStarters()
        {
            var starters = new List<Player>();
            if (_lineup != null)
            {
                foreach (Player player in _lineup)
                {
                    if (player != null)
                    {
                        starters.Add(player);
                    }
                }
            }

            return starters;
        }

        private List<Player> BenchPlayers()
        {
            var bench = new List<Player>();
            foreach (Player player in ManagedSquad().Players)
            {
                if (SlotOf(player.Id.Value) < 0)
                {
                    bench.Add(player);
                }
            }

            return bench;
        }

        private int SlotOf(int playerId)
        {
            if (_lineup != null)
            {
                for (int i = 0; i < _lineup.Length; i++)
                {
                    if (_lineup[i] != null && _lineup[i].Id.Value == playerId)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private bool IsStarting(int playerId)
        {
            return SlotOf(playerId) >= 0;
        }

        private void PlaceInSlot(int slot, Player player)
        {
            int from = SlotOf(player.Id.Value);
            if (from == slot)
            {
                return;
            }

            Player occupant = _lineup[slot];
            _lineup[slot] = player;
            if (from >= 0)
            {
                _lineup[from] = occupant;
            }

            CommitLineup();
        }

        private void ToggleStarter(int playerId)
        {
            int slot = SlotOf(playerId);
            if (slot >= 0)
            {
                _lineup[slot] = null;
                CommitLineup();
                return;
            }

            for (int i = 0; i < _lineup.Length; i++)
            {
                if (_lineup[i] == null)
                {
                    _lineup[i] = FindInSquad(playerId);
                    CommitLineup();
                    return;
                }
            }

            _lineupStatus = "The eleven is full — bench someone first.";
            Refresh();
        }

        private Player FindInSquad(int playerId)
        {
            foreach (Player player in ManagedSquad().Players)
            {
                if (player.Id.Value == playerId)
                {
                    return player;
                }
            }

            return null;
        }

        private void CommitLineup()
        {
            _season.SetStarters(_managedClub, CurrentStarters());
            Refresh();
        }

        private void ChangeFormation(string formationName)
        {
            foreach (Formation preset in Formation.Presets)
            {
                if (preset.Name == formationName)
                {
                    _formation = preset;
                    break;
                }
            }

            AutoPickStarters();
            _season.SetFormation(_managedClub, _formation);
            _season.SetStarters(_managedClub, CurrentStarters());
            Refresh();
        }

        private League BuildLeague(int count)
        {
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator()));
            var genRng = new SplitMix64RandomNumberGenerator((ulong)_seed ^ 0x5EEDD5EEDUL);
            return generator.Generate(count, genRng);
        }

        // ----- Season loop ---------------------------------------------------------------------------------

        private void AdvanceOneWeek()
        {
            if (_season == null || _season.IsComplete)
            {
                return;
            }

            _lastWeek = _season.AdvanceWeek(_simulator, _context, (ulong)_seed);
            _finances = _finances.PayWeeklyWages();
            CheckComplete();
            Refresh();
        }

        private void PlayToEnd()
        {
            if (_season == null)
            {
                return;
            }

            int guard = 0;
            while (!_season.IsComplete && guard < 1000)
            {
                WeekResult week = _season.AdvanceWeek(_simulator, _context, (ulong)_seed);
                _finances = _finances.PayWeeklyWages();
                if (week.Matches.Count > 0)
                {
                    _lastWeek = week;
                }

                guard++;
            }

            CheckComplete();
            Refresh();
        }

        private void CheckComplete()
        {
            if (_season.IsComplete && _verdict == null)
            {
                _verdict = new SeasonEvaluator().Evaluate(_season.Table, _managedClub, _target);
            }
        }

        // ----- Render --------------------------------------------------------------------------------------

        private void Refresh()
        {
            _body.Clear();
            if (_season == null)
            {
                return;
            }

            string managedName = _league.Clubs[_managedClub.Value].Name;

            VisualElement header = MakeCard();
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.Add(MakeLabel("YOU MANAGE  " + managedName.ToUpperInvariant(), 13, HarnessPalette.Accent, bold: true));
            top.Add(MakeLabel("Season " + _seasonNumber + "  ·  Week " + _season.CurrentRound + " / " + _season.RoundCount, 12, HarnessPalette.Muted));
            header.Add(top);
            header.Add(MakeLabel(
                "Board target: finish top " + _target.PromotionPosition + " to go up, stay above " +
                _target.SurvivalPosition + " to keep your job.", 11, HarnessPalette.Muted));

            // Finances line — cash and the weekly wage cost that eats it. Cash red when overdrawn.
            var moneyRow = new VisualElement();
            moneyRow.style.flexDirection = FlexDirection.Row;
            moneyRow.style.justifyContent = Justify.SpaceBetween;
            moneyRow.style.marginTop = 8;
            Color cashColor = _finances.Cash < 0 ? HarnessPalette.Loss : HarnessPalette.Accent;
            moneyRow.Add(MakeLabel("CASH  " + FormatValue(_finances.Cash), 15, cashColor, bold: true));
            Color wageColor = _finances.WageHeadroom < 0 ? HarnessPalette.Loss : HarnessPalette.Muted;
            moneyRow.Add(MakeLabel(
                "Wages " + FormatValue(_finances.WeeklyWageBill) + " / " + FormatValue(_finances.WeeklyWageBudget) +
                "/wk  ·  " + FormatValue(_finances.WageHeadroom) + "/wk free", 11, wageColor));
            header.Add(moneyRow);

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

            if (!_season.IsComplete)
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

            if (_retired != null && (_retired.Count > 0 || _arrived.Count > 0))
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

        private VisualElement BuildSummerCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("SUMMER " + _seasonNumber, 11, HarnessPalette.Muted, bold: true));

            if (_retired.Count > 0)
            {
                var outLine = new List<string>();
                foreach (Player p in _retired)
                {
                    outLine.Add(p.Name + " (" + PlayerRoles.Abbrev(p.Role) + " " + p.Age + ")");
                }

                card.Add(MakeLabel("Retired:  " + string.Join(",   ", outLine), 11, HarnessPalette.Loss));
            }

            if (_arrived.Count > 0)
            {
                var inLine = new List<string>();
                foreach (Player p in _arrived)
                {
                    int ovr = Mathf.RoundToInt((float)PlayerRatings.ForRole(p));
                    inLine.Add(p.Name + " (" + PlayerRoles.Abbrev(p.Role) + " " + p.Age + ", OVR " + ovr + ")");
                }

                card.Add(MakeLabel("Youth in:  " + string.Join(",   ", inLine), 11, HarnessPalette.Accent));
            }

            return card;
        }

        private VisualElement BuildLineupCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("LINEUP", 11, HarnessPalette.Muted, bold: true));

            int starting = CurrentStarters().Count;
            bool full = starting == _formation.Total;
            card.Add(MakeLabel(
                "Starting XI: " + starting + "/" + _formation.Total + "   ·   drag players on the pitch, or up from the bench",
                10, full ? HarnessPalette.Muted : HarnessPalette.Loss));

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
                if (presets[i].Name == _formation.Name)
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
            Vector2[] positions = SlotPositions();
            for (int i = 0; i < _formation.Total; i++)
            {
                Player player = _lineup != null && i < _lineup.Length ? _lineup[i] : null;
                PlayerRole role = _formation.Slots[i];
                VisualElement token = MakePitchToken(player, role, i, positions[i]);
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

            token.Add(MakeLabel(PlayerRoles.Abbrev(slotRole), 9, HarnessPalette.Muted, bold: true));
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
            foreach (Player player in BenchPlayers())
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
                chip.Add(MakeLabel(PlayerRoles.Abbrev(player.Role) + " " + Surname(player.Name), 10, HarnessPalette.Muted));
                chip.Add(MakeLabel("  " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)), 10, HarnessPalette.Accent, bold: true));

                RegisterDrag(chip, -1, player.Id.Value);
                row.Add(chip);
            }

            wrap.Add(row);
            return wrap;
        }

        private void RegisterDrag(VisualElement token, int slot, int playerId)
        {
            token.RegisterCallback<PointerDownEvent>(evt =>
            {
                _dragFromSlot = slot;
                _dragFromBenchId = slot >= 0 ? -1 : playerId;
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
            _dragGhost.Add(MakeLabel(PlayerRoles.Abbrev(player.Role), 9, HarnessPalette.Muted, bold: true));
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

        private void HandleDrop(Vector2 position)
        {
            _lineupStatus = null;
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

            Player dragged = _dragFromSlot >= 0
                ? (_dragFromSlot < _lineup.Length ? _lineup[_dragFromSlot] : null)
                : FindInSquad(_dragFromBenchId);

            if (dropSlot < 0)
            {
                if (_dragFromSlot >= 0)
                {
                    _lineup[_dragFromSlot] = null;
                    CommitLineup();
                }
                else
                {
                    Refresh();
                }
            }
            else if (dragged != null)
            {
                PlaceInSlot(dropSlot, dragged);
            }
            else
            {
                Refresh();
            }

            _dragFromSlot = -1;
            _dragFromBenchId = -1;
        }

        private Vector2[] SlotPositions()
        {
            var bands = new Dictionary<float, List<int>>();
            var y = new float[_formation.Total];
            for (int i = 0; i < _formation.Total; i++)
            {
                y[i] = BandY(_formation.Slots[i]);
                if (!bands.TryGetValue(y[i], out List<int> members))
                {
                    members = new List<int>();
                    bands[y[i]] = members;
                }

                members.Add(i);
            }

            var positions = new Vector2[_formation.Total];
            foreach (KeyValuePair<float, List<int>> band in bands)
            {
                List<int> members = band.Value;
                members.Sort((a, b) =>
                {
                    int byWidth = HorizontalKey(_formation.Slots[a]).CompareTo(HorizontalKey(_formation.Slots[b]));
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

            var mentality = new EnumField("Mentality", _tactics.Mentality);
            mentality.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics((Mentality)e.newValue, _tactics.Tempo, _tactics.Pressing, _tactics.Approach)));
            card.Add(mentality);

            var tempo = new EnumField("Tempo", _tactics.Tempo);
            tempo.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics(_tactics.Mentality, (Tempo)e.newValue, _tactics.Pressing, _tactics.Approach)));
            card.Add(tempo);

            var pressing = new EnumField("Pressing", _tactics.Pressing);
            pressing.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics(_tactics.Mentality, _tactics.Tempo, (Pressing)e.newValue, _tactics.Approach)));
            card.Add(pressing);

            var approach = new EnumField("Approach", _tactics.Approach);
            approach.RegisterValueChangedCallback(e =>
                ChangeTactics(new Tactics(_tactics.Mentality, _tactics.Tempo, _tactics.Pressing, (Approach)e.newValue)));
            card.Add(approach);

            ChanceProfile profile = ChanceProfile.FromTactics(_tactics);
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

        private void ChangeTactics(Tactics tactics)
        {
            _tactics = tactics;
            if (_season != null)
            {
                _season.SetTactics(_managedClub, _tactics);
            }

            Refresh();
        }

        private VisualElement BuildSquadCard()
        {
            Squad squad = ManagedSquad();
            string clubName = _league.Clubs[_managedClub.Value].Name;
            VisualElement card = MakeCard();
            int starting = CurrentStarters().Count;
            card.Add(MakeLabel(
                "YOUR SQUAD · " + clubName.ToUpperInvariant() + "  ·  " + starting + "/" + _formation.Total + " STARTING",
                11, HarnessPalette.Muted, bold: true));

            TeamStrength strength = squad != null
                ? new EffectiveStrengthBuilder().Build(CurrentStarters(), _tactics)
                : _league.Clubs[_managedClub.Value].Strength;
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

            bool canSell = WindowOpen();
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

                int playerId = player.Id.Value;
                var toggle = new Button(() => ToggleStarter(playerId)) { text = starting ? "★" : "·" };
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

                var left = MakeLabel(player.Name + "  ·  " + PlayerRoles.Abbrev(player.Role) + "  ·  " + player.Age, 11,
                    starting ? HarnessPalette.Chalk : HarnessPalette.Muted);
                left.style.flexGrow = 1;
                row.Add(left);

                var ovr = MakeLabel("OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)), 11, HarnessPalette.Accent, bold: true);
                ovr.style.marginRight = 8;
                row.Add(ovr);

                row.Add(TraitBadges(player));
                row.Add(BuildKeyStats(player));

                // Selling is only possible in an open window — the squad is otherwise locked for the season.
                if (canSell)
                {
                    Player target = player;
                    var sell = new Button(() => Sell(target)) { text = "Sell " + FormatValue(TransferService.Fee(player)) };
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
                Label chip = MakeLabel(key.Label + " " + value, 10, AttributeColor(value), value >= 85);
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

            return wrap;
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

        private VisualElement BuildTransferCard()
        {
            VisualElement card = MakeCard();

            TransferWindowPhase phase = WindowPhase();
            bool open = phase != TransferWindowPhase.Closed;
            string label = phase == TransferWindowPhase.Summer ? "SUMMER WINDOW · OPEN"
                : phase == TransferWindowPhase.Winter ? "WINTER WINDOW · OPEN"
                : "TRANSFER WINDOW · CLOSED";
            Color labelColor = open ? HarnessPalette.Win : HarnessPalette.Muted;

            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.justifyContent = Justify.SpaceBetween;
            head.Add(MakeLabel("TRANSFER MARKET — " + _market.Count + " PROSPECTS", 11, HarnessPalette.Muted, bold: true));
            head.Add(MakeLabel(label, 11, labelColor, bold: true));
            card.Add(head);

            card.Add(MakeLabel(
                open
                    ? (_reveal ? "Revealing true potential (dev)." : "OVR is current ability; potential is scout-masked — trust the band, take the punt.")
                    : "The market is closed. It opens in the summer (pre-season) and at the winter break.",
                10, HarnessPalette.Muted));

            if (!string.IsNullOrEmpty(_transferStatus))
            {
                card.Add(MakeLabel(_transferStatus, 11, HarnessPalette.Chalk));
            }

            if (!open)
            {
                return card;
            }

            // Filter the shortlist by specific role (left back, right back, …) and by an age range.
            var roleChoices = new List<string> { "All positions" };
            foreach (PlayerRole role in FilterRoles)
            {
                roleChoices.Add(RoleName(role));
            }

            var roleFilter = new DropdownField("Position", roleChoices, RoleFilterIndex());
            roleFilter.RegisterValueChangedCallback(e =>
            {
                int index = roleChoices.IndexOf(e.newValue);
                _marketRoleFilter = index <= 0 ? (PlayerRole?)null : FilterRoles[index - 1];
                Refresh();
            });
            card.Add(roleFilter);

            var ageRow = new VisualElement();
            ageRow.style.flexDirection = FlexDirection.Row;
            var minAge = new IntegerField("Min age") { value = _marketMinAge };
            minAge.style.flexGrow = 1;
            minAge.RegisterValueChangedCallback(e =>
            {
                _marketMinAge = e.newValue;
                Refresh();
            });
            ageRow.Add(minAge);
            var maxAge = new IntegerField("Max age") { value = _marketMaxAge };
            maxAge.style.flexGrow = 1;
            maxAge.style.marginLeft = 8;
            maxAge.RegisterValueChangedCallback(e =>
            {
                _marketMaxAge = e.newValue;
                Refresh();
            });
            ageRow.Add(maxAge);
            card.Add(ageRow);

            int shown = 0;
            foreach (Player player in ByOverallDescending(_market))
            {
                if (_marketRoleFilter != null && player.Role != _marketRoleFilter.Value)
                {
                    continue;
                }

                if (player.Age < _marketMinAge || player.Age > _marketMaxAge)
                {
                    continue;
                }

                shown++;
                ScoutReport report = _scout.Observe(player, _accuracy);

                var row = new VisualElement();
                row.style.paddingTop = 6;
                row.style.paddingBottom = 6;
                row.style.borderBottomWidth = 1;
                row.style.borderBottomColor = HarnessPalette.PitchLine;

                var line = new VisualElement();
                line.style.flexDirection = FlexDirection.Row;
                line.style.alignItems = Align.Center;

                var name = MakeLabel(player.Name + "  ·  " + PlayerRoles.Abbrev(player.Role) + "  ·  " + player.Age, 12, HarnessPalette.Chalk, bold: true);
                name.style.flexGrow = 1;
                line.Add(name);
                line.Add(TraitBadges(player));
                line.Add(MakeLabel("OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)) + "   ", 12, HarnessPalette.Accent, bold: true));
                line.Add(MakeLabel(FormatValue(PlayerValuation.Value(player)) + " · " + FormatValue(PlayerWage.Weekly(player)) + "/wk   ", 11, HarnessPalette.Muted));

                Player target = player;
                var sign = new Button(() => Sign(target)) { text = "Sign " + FormatValue(TransferService.Fee(player)) };
                StyleActionButton(sign, HarnessPalette.Accent);
                line.Add(sign);
                row.Add(line);

                string potential = "Potential " + report.PotentialLow + "–" + report.PotentialHigh;
                if (_reveal)
                {
                    potential += "   (true " + player.HiddenPotential + ")";
                }

                row.Add(MakeLabel(potential, 11, HarnessPalette.Accent));
                row.Add(MakeLabel(FormatScoutAttributes(report), 10, HarnessPalette.Muted));
                card.Add(row);
            }

            if (shown == 0)
            {
                card.Add(MakeLabel("No prospects match this filter.", 10, HarnessPalette.Muted));
            }

            return card;
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
                default: return PlayerRoles.Abbrev(role);
            }
        }

        private static List<Player> ByOverallDescending(IReadOnlyList<Player> players)
        {
            var sorted = new List<Player>(players);
            sorted.Sort((a, b) =>
            {
                int byRating = PlayerRatings.ForRole(b).CompareTo(PlayerRatings.ForRole(a));
                return byRating != 0 ? byRating : a.Id.Value.CompareTo(b.Id.Value);
            });
            return sorted;
        }

        private static string FormatScoutAttributes(ScoutReport report)
        {
            var parts = new List<string>(report.KeyAttributes.Count);
            foreach (AttributeEstimate estimate in report.KeyAttributes)
            {
                string band = estimate.Low == estimate.High
                    ? estimate.Low.ToString()
                    : estimate.Low + "–" + estimate.High;
                parts.Add(estimate.Label + " " + band);
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

            foreach (MatchResult match in _lastWeek.Matches)
            {
                bool involvesManaged = match.Home == _managedClub || match.Away == _managedClub;
                Color scoreColor = involvesManaged ? HarnessPalette.Accent : HarnessPalette.Chalk;

                var block = new VisualElement();
                block.style.marginTop = 6;

                string homeName = _league.Clubs[match.Home.Value].Name;
                string awayName = _league.Clubs[match.Away.Value].Name;
                block.Add(MakeLabel(
                    homeName + "  " + match.HomeGoals + " - " + match.AwayGoals + "  " + awayName,
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
                parts.Add(_league.Clubs[match.Home.Value].Name + "  " + string.Join(", ", home));
            }

            if (away.Count > 0)
            {
                parts.Add(_league.Clubs[match.Away.Value].Name + "  " + string.Join(", ", away));
            }

            return string.Join("      ", parts);
        }

        private string ScorerEntry(ClubId club, MatchEvent goal)
        {
            string name = ScorerSurname(club, goal.Scorer);
            return name.Length > 0 ? name + " " + goal.Minute + "'" : goal.Minute + "'";
        }

        private string ScorerSurname(ClubId club, PlayerId? scorer)
        {
            if (scorer == null)
            {
                return string.Empty;
            }

            // The managed club's live roster is read from the season (it may hold a just-signed scorer);
            // every other club reads from the league.
            Squad squad = club == _managedClub ? _season.SquadOf(club) : _league.Clubs[club.Value].Squad;
            if (squad == null)
            {
                return string.Empty;
            }

            foreach (Player player in squad.Players)
            {
                if (player.Id == scorer.Value)
                {
                    int space = player.Name.LastIndexOf(' ');
                    return space >= 0 ? player.Name.Substring(space + 1) : player.Name;
                }
            }

            return string.Empty;
        }

        private VisualElement BuildVerdictBanner()
        {
            Color color = _verdict == SeasonVerdict.Promoted ? HarnessPalette.Win
                : _verdict == SeasonVerdict.Sacked ? HarnessPalette.Loss : HarnessPalette.Chalk;
            string headline = _verdict == SeasonVerdict.Promoted ? "PROMOTED"
                : _verdict == SeasonVerdict.Sacked ? "SACKED" : "RETAINED";

            VisualElement banner = MakeCard();
            banner.style.borderLeftWidth = 4;
            banner.style.borderLeftColor = color;
            banner.style.marginTop = 8;

            int position = FindManagedPosition();
            banner.Add(MakeLabel(headline, 24, color, bold: true));
            banner.Add(MakeLabel(
                _league.Clubs[_managedClub.Value].Name + " finished " + Ordinal(position) + ".",
                12, HarnessPalette.Muted));
            return banner;
        }

        private int FindManagedPosition()
        {
            IReadOnlyList<LeagueTableRow> ordered = _season.Table.Ordered();
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Club == _managedClub)
                {
                    return i + 1;
                }
            }

            return ordered.Count;
        }

        private VisualElement BuildTableCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("LEAGUE TABLE", 11, HarnessPalette.Muted, bold: true));

            string[] heads = { "#", "Club", "P", "W", "D", "L", "GF", "GA", "GD", "Pts" };
            card.Add(MakeRow(heads, HarnessPalette.Muted, bold: true, background: new Color(0, 0, 0, 0)));

            IReadOnlyList<LeagueTableRow> table = _season.Table.Ordered();
            for (int i = 0; i < table.Count; i++)
            {
                LeagueTableRow row = table[i];
                int position = i + 1;
                bool isManaged = row.Club == _managedClub;

                Color background = isManaged ? Tint(HarnessPalette.Accent, 0.16f)
                    : position <= _target.PromotionPosition ? Tint(HarnessPalette.Win, 0.07f)
                    : position > _target.SurvivalPosition ? Tint(HarnessPalette.Loss, 0.07f)
                    : new Color(0, 0, 0, 0);

                string[] cells =
                {
                    position.ToString(), _league.Clubs[row.Club.Value].Name, row.Played.ToString(),
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

        private static string FormatValue(long value)
        {
            if (value < 0)
            {
                return "-" + FormatValue(-value);
            }

            if (value >= 1_000_000)
            {
                return "€" + (value / 1_000_000.0).ToString("0.0") + "M";
            }

            if (value >= 1_000)
            {
                return "€" + (value / 1_000) + "k";
            }

            return "€" + value;
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
