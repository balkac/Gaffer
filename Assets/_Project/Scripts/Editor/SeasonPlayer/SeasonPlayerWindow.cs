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
using Gaffer.Domain.Players;
using Gaffer.Editor.Balance;
using Gaffer.Editor.Harness;
using Gaffer.Infrastructure.Configuration;
using Gaffer.UserData;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Position = Gaffer.Domain.Players.Position;

namespace Gaffer.Editor.SeasonPlayer
{
    /// <summary>
    /// An early playable demo: manage one club through league seasons on the real Application core.
    /// Advance week by week, watch the table move, and get the board's verdict at the end — promoted,
    /// retained, or sacked — then Start Next Season to roll the whole league on a year (squads age,
    /// develop and renew), so gems grow and veterans fade across a run. Styled in the ART_STYLE broadcast
    /// identity. Not shipped; a preview of the run the real UI (Faz 7) will present.
    ///
    /// <para><b>A view over <see cref="RunSession"/>.</b> This window and the Management window each used
    /// to own a private copy of the run loop, and the copies had drifted: that one paid the weekly wage
    /// bill and ticked drama, this one did neither, and this one read the managed squad off the
    /// generation-time league rather than the live season. Both now send commands to one session and
    /// replay its outcomes (ARCHITECTURE §8), so this window has the economy and the drama beats it was
    /// silently missing — the divergence was the bug, not the feature set.</para>
    /// </summary>
    public sealed class SeasonPlayerWindow : EditorWindow
    {
        private int _teamCount = 20;
        private long _seed = 20260707L;
        private int _managedIndex = 15;
        private int _promotionPosition = 3;
        private int _survivalPosition = 17;
        private long _startingCash = 6_000_000L;
        private long _wageBudget = 160_000L;

        // The shape and setup the run is on, re-synced from every outcome so a restart or a resume picks up
        // what the manager was actually playing (a save carries neither).
        private Tactics _tactics = Tactics.Balanced;
        private Formation _formation = Formation.F442;

        private RunSession _session;
        private LineupOutcome _lineup;
        private WeekOutcome _lastWeek;
        private SeasonRollover _summer;

        private int _dragFromSlot = -1;
        private int _dragPlayerId = -1;
        private string _lineupStatus;
        private VisualElement _dragGhost;
        private readonly List<VisualElement> _slotTokens = new List<VisualElement>();

        private string _saveStatus;
        private string _dramaStatus;

        private SimulationBalanceSO _simulationBalance;
        private DevelopmentBalanceSO _developmentBalance;
        private RenewalBalanceSO _renewalBalance;

        private VisualElement _body;

        private static string SavePath => Path.Combine(UnityEngine.Application.persistentDataPath, "gaffer-run.json");

        [MenuItem("Gaffer/Season Player")]
        public static void ShowWindow()
        {
            SeasonPlayerWindow window = GetWindow<SeasonPlayerWindow>();
            window.titleContent = new GUIContent("Season Player");
            window.minSize = new Vector2(540, 680);
        }

        public void CreateGUI()
        {
            // Pre-assign the default balance assets (created on first use), so the Balance fields come filled
            // in and every knob is editable without hand-creating an SO.
            _simulationBalance = _simulationBalance != null ? _simulationBalance : BalanceAssets.Simulation();
            _developmentBalance = _developmentBalance != null ? _developmentBalance : BalanceAssets.Development();
            _renewalBalance = _renewalBalance != null ? _renewalBalance : BalanceAssets.Renewal();

            var scroll = new ScrollView();
            scroll.style.backgroundColor = HarnessPalette.Pitch;
            rootVisualElement.Add(scroll);

            var page = new VisualElement();
            SetPadding(page, 20);
            scroll.Add(page);

            Label title = MakeLabel("SEASON PLAYER", 22, HarnessPalette.Chalk, bold: true);
            title.style.letterSpacing = 2f;
            page.Add(title);
            page.Add(MakeLabel("Manage a club season after season — squads age and develop between them", 11, HarnessPalette.Muted));

            page.Add(BuildSetup());

            _body = new VisualElement();
            _body.style.marginTop = 6;
            page.Add(_body);

            _body.Add(MakeCard());
            ((VisualElement)_body[0]).Add(MakeLabel("Set it up, then Start Season.", 12, HarnessPalette.Muted));
        }

        // The drag ghost is parented to the window root, outside the body Refresh rebuilds, so a window
        // closed mid-drag would otherwise leave it behind (UNITY.md §5).
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

            // The economy the run is played under — the wage bill leaves this cash every single week
            // (GDD §4.4). This window used to skip the payment entirely, so its books never moved.
            Label money = MakeLabel("Economy", 10, HarnessPalette.Muted, bold: true);
            money.style.marginTop = 6;
            card.Add(money);

            var cash = new LongField("Transfer cash (€)") { value = _startingCash };
            cash.RegisterValueChangedCallback(e => _startingCash = e.newValue);
            card.Add(cash);

            var wageBudget = new LongField("Wage budget (€/wk)") { value = _wageBudget };
            wageBudget.RegisterValueChangedCallback(e => _wageBudget = e.newValue);
            card.Add(wageBudget);

            // Optional balance assets — assign a Balance SO to retune the run, leave empty for the calibrated
            // defaults. Simulation applies on Start Season; development and renewal apply on Start Next Season.
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

        // ----- The run's wiring seam -----------------------------------------------------------------------

        private RunSetup Setup()
        {
            return new RunSetup
            {
                TeamCount = _teamCount,
                Seed = (ulong)_seed,
                ManagedClubIndex = _managedIndex,
                PromotionPosition = _promotionPosition,
                SurvivalPosition = _survivalPosition,
                StartingCash = _startingCash,
                WeeklyWageBudget = _wageBudget,
                Formation = _formation,
                Tactics = _tactics,
            };
        }

        // Only the three balance assets this window authors are overridden; everything else — drama, morale,
        // the economy, the trait and drama catalogs — takes the calibrated default, which is what the factory
        // fills a null with (ARCHITECTURE §7). The Management window is where every knob is authored.
        private RunBalance Balance()
        {
            return new RunBalance
            {
                Simulation = _simulationBalance != null ? _simulationBalance.ToSettings() : MatchSimulationSettings.Default,
                TacticsBalance = _simulationBalance != null ? _simulationBalance.ToTacticsSettings() : TacticsSettings.Default,
                Scorer = _simulationBalance != null ? _simulationBalance.ToScorerWeights() : ScorerWeights.Default,
                Development = _developmentBalance != null ? _developmentBalance.ToSettings() : DevelopmentSettings.Default,
                Renewal = _renewalBalance != null ? _renewalBalance.ToSettings() : RenewalSettings.Default,
            };
        }

        private void StartSeason()
        {
            Result<RunSession> started = RunSessionFactory.Start(Setup(), Balance());
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
            Replay(session.Lineup());
            _lastWeek = null;
            _summer = null;
            _lineupStatus = null;
            _dramaStatus = null;
            _saveStatus = status;
            Refresh();
        }

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

        // Writes the whole run — every club's squad, the season number, the table, and the seed — to a JSON
        // file through the real Infrastructure adapter (NewtonsoftJsonSerializer + JsonSaveStore), the same
        // path the shipped game will use. The session captures the document with the managed club's live
        // roster already folded back in, which is the ordering a window used to have to remember.
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

        // Reads the run back, migrates it to the current schema, and resumes it on the save's own match seed.
        // Formation, tactics, the economy and drama state are session settings, not saved, so they come from
        // the setup — the run state (rosters, table, season number) is what persists.
        private void LoadRun()
        {
            Result<SeasonSaveData> loaded = SaveStore().Load(SavePath);
            if (loaded.IsFailure)
            {
                _saveStatus = loaded.Error;
                Refresh();
                return;
            }

            Result<RunSession> resumed = RunSessionFactory.Resume(Setup(), Balance(), loaded.Value);
            if (resumed.IsFailure)
            {
                _saveStatus = resumed.Error;
                Refresh();
                return;
            }

            Adopt(resumed.Value, "Loaded run from " + SavePath);
        }

        private static JsonSaveStore SaveStore()
        {
            return new JsonSaveStore(new NewtonsoftJsonSerializer(), new SaveMigrator());
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

        // The week's outcome carries the position, the losing run, the wages paid and the verdict — every
        // number this window used to re-derive by walking the season's own table and result history.
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
            _dramaStatus = null;
            Refresh();
        }

        // ----- Drama ---------------------------------------------------------------------------------------

        private void ResolveDrama(int choiceIndex)
        {
            Result<DramaResolution> result = _session.ResolveDrama(choiceIndex);
            if (result.IsFailure)
            {
                _dramaStatus = result.Error;
                Refresh();
                return;
            }

            DramaResolution resolution = result.Value;
            Replay(resolution.Lineup);
            _dramaStatus = Describe(resolution);
            Refresh();
        }

        private static string Describe(DramaResolution resolution)
        {
            string text = Humanize(resolution.EventId.Value) + " — resolved.";
            if (resolution.CashDelta != 0)
            {
                text += " Cash " + (resolution.CashDelta > 0 ? "+" : "") + FormatValue(resolution.CashDelta) + ".";
            }

            if (resolution.SoldPlayer != null)
            {
                text += " " + resolution.SoldPlayer.Name + " sold for " + FormatValue(resolution.SaleFee) + ".";
            }

            if (resolution.RebuiltPlayer != null)
            {
                text += " " + resolution.RebuiltPlayer.Name + " is now a " + Humanize(resolution.GrantedTrait.Value) + ".";
            }

            return text;
        }

        // Dev-tool copy: the shipped UI reads localized text through the event's keys; the workbench
        // humanizes the slugs so the loop is playable today (see HarnessLabels on why that is allowed here).
        private static string Humanize(string slug)
        {
            string spaced = slug.Replace('-', ' ').Replace('_', ' ');
            return spaced.Length == 0 ? spaced : char.ToUpperInvariant(spaced[0]) + spaced.Substring(1);
        }

        private static string ChoiceLabel(string labelKey)
        {
            int lastDot = labelKey.LastIndexOf('.');
            return Humanize(lastDot >= 0 ? labelKey.Substring(lastDot + 1) : labelKey);
        }

        private VisualElement BuildDramaCard(PendingDrama pending)
        {
            VisualElement card = MakeCard();
            card.style.borderLeftWidth = 3;
            card.style.borderLeftColor = HarnessPalette.Accent;

            card.Add(MakeLabel("DRAMA · WEEK " + _session.PlayedRounds, 11, HarnessPalette.Accent, bold: true));
            card.Add(MakeLabel(Humanize(pending.Event.Id.Value).ToUpperInvariant(), 15, HarnessPalette.Chalk, bold: true));

            if (pending.Subject != null)
            {
                Player subject = pending.Subject;
                card.Add(MakeLabel(
                    subject.Name + "  ·  " + HarnessLabels.RoleLabel(subject.Role) + "  ·  " + subject.Age +
                    "  ·  OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(subject)), 11, HarnessPalette.Chalk));
            }

            card.Add(MakeLabel("The decision is yours — it will be felt on the pitch and in the books.", 10, HarnessPalette.Muted));

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 8;
            for (int i = 0; i < pending.Event.Choices.Count; i++)
            {
                int index = i;
                var choice = new Button(() => ResolveDrama(index)) { text = ChoiceLabel(pending.Event.Choices[i].LabelKey) };
                choice.style.flexGrow = 1;
                choice.style.height = 26;
                if (i > 0)
                {
                    choice.style.marginLeft = 6;
                }

                choice.style.backgroundColor = HarnessPalette.Accent;
                choice.style.color = HarnessPalette.Pitch;
                choice.style.unityFontStyleAndWeight = FontStyle.Bold;
                SetRadius(choice, 4);
                buttons.Add(choice);
            }

            card.Add(buttons);
            return card;
        }

        // ----- Render --------------------------------------------------------------------------------------

        private void Refresh()
        {
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

            // Replayed from the last week's outcome, not re-derived from the live table.
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

                var next = new Button(StartNextSeason) { text = "Start Next Season  →  age + develop every squad" };
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

            if (_summer != null && (_summer.Retired.Count > 0 || _summer.Arrived.Count > 0))
            {
                _body.Add(BuildSummerCard());
            }

            _body.Add(BuildLineupCard());
            _body.Add(BuildTacticsCard());
            _body.Add(BuildSquadCard());
            _body.Add(BuildTableCard());

            if (_lastWeek != null)
            {
                _body.Add(BuildLastWeekCard());
            }
        }

        private static string FormOf(int lossStreak)
        {
            if (lossStreak == 0)
            {
                return "no losing run";
            }

            return lossStreak == 1 ? "1 defeat on the bounce" : lossStreak + " defeats on the bounce";
        }

        // The summer's comings and goings for your club — who retired, who came through the academy.
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

            card.Add(MakeLabel(
                "Starting XI: " + _lineup.Starters.Count + "/" + _lineup.Formation.Total +
                "   ·   drag players on the pitch, or up from the bench",
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

            // Halfway line, for a broadcast-pitch read.
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

        // Pointer-capture drag: pick up a token (from a slot or the bench), let it follow the cursor, and on
        // release drop it onto whichever pitch slot is under the pointer — or off the pitch to bench a starter.
        // The callbacks live exactly as long as the token they sit on: every one of these elements is discarded
        // by the next Refresh's _body.Clear(), which is the removal UNITY.md §5 asks for.
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

        // A floating copy on the window root that follows the cursor while dragging, so both a pitch token
        // and a bench chip read as "picked up" wherever they live in the layout.
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
                // Skip the slot we picked up from — its token follows the cursor, so it would always match.
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

        // A believable pitch layout: each slot gets a vertical band from its role and an even horizontal
        // spread within that band, with wide roles pushed to the touchlines.
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

            // Mentality and pressing move the strength axes above; tempo and approach shape the chances.
            // Shown as a delta from a balanced setup so the trade-off reads at a glance — and taken off the
            // outcome, so it is the profile the run's own tactics balance produces, not the untuned default.
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

        private void ChangeTactics(Tactics tactics)
        {
            Apply(_session.SetTactics(tactics));
        }

        private VisualElement BuildSquadCard()
        {
            // The live roster off the session, not the generation-time league: a squad that drama or a
            // rollover has changed is reflected here at once. Reading _league.Clubs[i].Squad showed the
            // roster the world was generated with, however many seasons ago that was.
            Squad squad = _session.Squad;
            VisualElement card = MakeCard();
            card.Add(MakeLabel(
                "YOUR SQUAD · " + _session.ManagedClubName.ToUpperInvariant() + "  ·  " +
                _lineup.Starters.Count + "/" + _lineup.Formation.Total + " STARTING",
                11, HarnessPalette.Muted, bold: true));

            // The managed club's axes reflect the chosen eleven and live tactics — the same derivation the
            // season runs each week, through the run's own trait catalog, so what you see takes the field.
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

                // General rating (OVR) beside the name — the number that moves as a player develops or ages.
                var ovr = MakeLabel("OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)), 11, HarnessPalette.Accent, bold: true);
                ovr.style.marginRight = 8;
                row.Add(ovr);

                row.Add(BuildKeyStats(player));

                card.Add(row);
            }
        }

        private bool IsStarting(int playerId)
        {
            IReadOnlyList<Player> slots = _lineup != null ? _lineup.Slots : null;
            if (slots == null)
            {
                return false;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Id.Value == playerId)
                {
                    return true;
                }
            }

            return false;
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

        // Shows the role's key attributes (RoleKeyAttributes), each coloured by value on the single accent
        // ramp from ART_STYLE §4.1 — the eye goes to the strong numbers without a second bright colour.
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
                return new Color(0.616f, 0.667f, 0.643f); // #9DAAA4 — soluk
            }

            if (value >= 40)
            {
                return HarnessPalette.Muted;
            }

            return new Color(0.275f, 0.337f, 0.310f); // #46564F — en sönük
        }

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

        // "Ashfield  Doe 23', Roe 67'     Brackenmoor  Poe 81'" — named scorers per side, in minute order.
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

            // The final standing is on the week that ended the season. A season already finished when the
            // window met it (a save loaded at full time) has no such outcome, so it falls back to the
            // session's own render-time query.
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
