using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Editor.Harness;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Position = Gaffer.Domain.Players.Position;

namespace Gaffer.Editor.SeasonPlayer
{
    /// <summary>
    /// An early playable demo: manage one club through a full league season on the real Application
    /// core. Advance week by week, watch the table move, and get the board's verdict at the end —
    /// promoted, retained, or sacked. Styled in the ART_STYLE broadcast identity. Not shipped; a
    /// preview of the run the real UI (Faz 7) will present.
    /// </summary>
    public sealed class SeasonPlayerWindow : EditorWindow
    {
        private static readonly string[] ClubNames =
        {
            "Ashfield United", "Brackenmoor", "Coldharbour City", "Dunmore Athletic",
            "Elmspur Rovers", "Fenwick Town", "Gravesend", "Harrowgate",
            "Ironbridge", "Keswick Vale", "Langford City", "Marrowfield",
            "Northcliff", "Oakhaven", "Pemberton", "Quarrydale",
            "Redmarsh", "Stonebury", "Thornwood", "Uplyme United",
            "Vardenfell", "Westgate Albion", "Yarmouth Bay", "Ravensden",
        };

        private int _teamCount = 20;
        private long _seed = 20260707L;
        private int _managedIndex = 15;
        private int _promotionPosition = 3;
        private int _survivalPosition = 17;
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

        private VisualElement _body;

        [MenuItem("Gaffer/Season Player")]
        public static void ShowWindow()
        {
            SeasonPlayerWindow window = GetWindow<SeasonPlayerWindow>();
            window.titleContent = new GUIContent("Season Player");
            window.minSize = new Vector2(540, 680);
        }

        public void CreateGUI()
        {
            var scroll = new ScrollView();
            scroll.style.backgroundColor = HarnessPalette.Pitch;
            rootVisualElement.Add(scroll);

            var page = new VisualElement();
            SetPadding(page, 20);
            scroll.Add(page);

            Label title = MakeLabel("SEASON PLAYER", 22, HarnessPalette.Chalk, bold: true);
            title.style.letterSpacing = 2f;
            page.Add(title);
            page.Add(MakeLabel("Manage a club through one full season", 11, HarnessPalette.Muted));

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

            var start = new Button(StartSeason) { text = "Start Season" };
            start.style.backgroundColor = HarnessPalette.Accent;
            start.style.color = HarnessPalette.Pitch;
            start.style.unityFontStyleAndWeight = FontStyle.Bold;
            start.style.height = 30;
            start.style.marginTop = 10;
            SetRadius(start, 6);
            card.Add(start);

            return card;
        }

        private void StartSeason()
        {
            int count = Mathf.Clamp(_teamCount, 4, ClubNames.Length);
            _league = BuildLeague(count);
            _season = new LeagueSeason(_league);
            _simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
            _context = new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false);
            _managedClub = new ClubId(Mathf.Clamp(_managedIndex, 0, count - 1));
            _target = new BoardTarget(_promotionPosition, _survivalPosition);
            AutoPickStarters();
            _season.SetFormation(_managedClub, _formation);
            _season.SetStarters(_managedClub, CurrentStarters());
            _season.SetTactics(_managedClub, _tactics);
            _verdict = null;
            _lastWeek = null;

            Refresh();
        }

        private Squad ManagedSquad()
        {
            return _league.Clubs[_managedClub.Value].Squad;
        }

        // Fills each formation slot with the best eleven for the current formation — the default a manager tweaks.
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

        // Drops a player into a slot: if he was already on the pitch the two swap; if he came off the bench he
        // takes the slot and whoever was there is benched.
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
            // Each club now fields a generated squad; its match strength is derived from the players
            // (BuildEffectiveStrength), so the table's spread emerges from the rosters, not a scalar.
            // A separate rng stream keeps squad generation from disturbing match determinism.
            var squadGenerator = new SquadGenerator(new PlayerGenerator());
            var strengthBuilder = new EffectiveStrengthBuilder();
            var genRng = new SplitMix64RandomNumberGenerator((ulong)_seed ^ 0x5EEDD5EEDUL);

            var clubs = new List<Club>(count);
            for (int i = 0; i < count; i++)
            {
                GenerationContext context = ContextForRank(i, count);
                Squad squad = squadGenerator.Generate(i * SquadGenerator.SquadSize, context, genRng);
                TeamStrength strength = strengthBuilder.Build(squad);
                clubs.Add(new Club(new ClubId(i), ClubNames[i], squad, strength));
            }

            return new League("Gaffer League", clubs);
        }

        private static GenerationContext ContextForRank(int rank, int count)
        {
            // Top clubs draw from a higher ability band, bottom clubs a lower one — a believable spread.
            double t = count <= 1 ? 0.0 : (double)rank / (count - 1);
            int centre = Mathf.RoundToInt(72.0f - (float)t * (72.0f - 46.0f));
            return new GenerationContext
            {
                MinAbility = (byte)Mathf.Clamp(centre - 10, 1, 99),
                MaxAbility = (byte)Mathf.Clamp(centre + 10, 1, 99),
            };
        }

        private void AdvanceOneWeek()
        {
            if (_season == null || _season.IsComplete)
            {
                return;
            }

            _lastWeek = _season.AdvanceWeek(_simulator, _context, (ulong)_seed);
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
            top.Add(MakeLabel("Week " + _season.CurrentRound + " / " + _season.RoundCount, 12, HarnessPalette.Muted));
            header.Add(top);
            header.Add(MakeLabel(
                "Board target: finish top " + _target.PromotionPosition + " to go up, stay above " +
                _target.SurvivalPosition + " to keep your job.", 11, HarnessPalette.Muted));
            _body.Add(header);

            if (!_season.IsComplete)
            {
                var controls = new VisualElement();
                controls.style.flexDirection = FlexDirection.Row;
                controls.style.marginTop = 8;

                var advance = new Button(AdvanceOneWeek) { text = "Advance Week" };
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

                RegisterDrag(chip, -1, player.Id.Value);
                row.Add(chip);
            }

            wrap.Add(row);
            return wrap;
        }

        // Pointer-capture drag: pick up a token (from a slot or the bench), let it follow the cursor, and on
        // release drop it onto whichever pitch slot is under the pointer — or off the pitch to bench a starter.
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

            Player dragged = _dragFromSlot >= 0
                ? (_dragFromSlot < _lineup.Length ? _lineup[_dragFromSlot] : null)
                : FindInSquad(_dragFromBenchId);

            if (dropSlot < 0)
            {
                // Dropped off the slots: bench the player if he was on the pitch.
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

        // A believable pitch layout: each slot gets a vertical band from its role and an even horizontal
        // spread within that band, with wide roles pushed to the touchlines.
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

            // Mentality and pressing move the strength axes above; tempo and approach shape the chances.
            // Shown as a delta from a balanced setup so the trade-off reads at a glance.
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
            Club club = _league.Clubs[_managedClub.Value];
            VisualElement card = MakeCard();
            int starting = CurrentStarters().Count;
            card.Add(MakeLabel(
                "YOUR SQUAD · " + club.Name.ToUpperInvariant() + "  ·  " + starting + "/" + _formation.Total + " STARTING",
                11, HarnessPalette.Muted, bold: true));

            // The managed club's axes reflect the chosen eleven and live tactics — the same derivation the
            // season runs each week, so what you see is what takes the field.
            TeamStrength strength = club.Squad != null
                ? new EffectiveStrengthBuilder().Build(CurrentStarters(), _tactics)
                : club.Strength;
            var axes = new VisualElement();
            axes.style.flexDirection = FlexDirection.Row;
            axes.style.marginTop = 6;
            axes.style.marginBottom = 4;
            axes.Add(MakeAxisPill("ATK", strength.Attack));
            axes.Add(MakeAxisPill("MID", strength.Midfield));
            axes.Add(MakeAxisPill("DEF", strength.Defence));
            card.Add(axes);

            if (club.Squad != null)
            {
                AppendSquadLine(card, "GOALKEEPERS", club.Squad, Position.Goalkeeper);
                AppendSquadLine(card, "DEFENDERS", club.Squad, Position.Defender);
                AppendSquadLine(card, "MIDFIELDERS", club.Squad, Position.Midfielder);
                AppendSquadLine(card, "FORWARDS", club.Squad, Position.Forward);
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
                row.Add(BuildKeyStats(player));

                card.Add(row);
            }
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
                Label chip = MakeLabel(key.Label + " " + value, 10, AttributeColor(value), value >= 85);
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

            Squad squad = _league.Clubs[club.Value].Squad;
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
