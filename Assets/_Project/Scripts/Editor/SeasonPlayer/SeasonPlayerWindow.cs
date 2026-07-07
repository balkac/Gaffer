using System.Collections.Generic;
using System.Text;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Editor.Harness;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

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

        private League _league;
        private LeagueSeason _season;
        private ClubId _managedClub;
        private BoardTarget _target;
        private MatchSimulator _simulator;
        private MatchContext _context;
        private SplitMix64RandomNumberGenerator _rng;
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
            _rng = new SplitMix64RandomNumberGenerator((ulong)_seed);
            _simulator = new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver());
            _context = new MatchContext(MatchImportance.Normal, 12000, isTitleDecider: false, isRivalry: false);
            _managedClub = new ClubId(Mathf.Clamp(_managedIndex, 0, count - 1));
            _target = new BoardTarget(_promotionPosition, _survivalPosition);
            _verdict = null;
            _lastWeek = null;

            Refresh();
        }

        private League BuildLeague(int count)
        {
            var clubs = new List<Club>(count);
            for (int i = 0; i < count; i++)
            {
                double quality = 70.0 - i * (24.0 / (count - 1));
                clubs.Add(new Club(new ClubId(i), ClubNames[i], new TeamStrength(quality, quality, quality)));
            }

            return new League("Gaffer League", clubs);
        }

        private void AdvanceOneWeek()
        {
            if (_season == null || _season.IsComplete)
            {
                return;
            }

            _lastWeek = _season.AdvanceWeek(_simulator, _context, _rng);
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
                WeekResult week = _season.AdvanceWeek(_simulator, _context, _rng);
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

            _body.Add(BuildTableCard());

            if (_lastWeek != null)
            {
                _body.Add(BuildLastWeekCard());
            }
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

                string minutes = FormatGoalMinutes(match);
                if (minutes.Length > 0)
                {
                    block.Add(MakeLabel(minutes, 10, HarnessPalette.Muted));
                }

                card.Add(block);
            }

            return card;
        }

        private string FormatGoalMinutes(MatchResult match)
        {
            var home = new List<int>();
            var away = new List<int>();
            foreach (MatchEvent matchEvent in match.Events)
            {
                if (matchEvent.Kind != MatchEventKind.Goal)
                {
                    continue;
                }

                if (matchEvent.Side == TeamSide.Home)
                {
                    home.Add(matchEvent.Minute);
                }
                else
                {
                    away.Add(matchEvent.Minute);
                }
            }

            if (home.Count == 0 && away.Count == 0)
            {
                return string.Empty;
            }

            home.Sort();
            away.Sort();

            var parts = new List<string>();
            if (home.Count > 0)
            {
                parts.Add(_league.Clubs[match.Home.Value].Name + " " + MinutesText(home));
            }

            if (away.Count > 0)
            {
                parts.Add(_league.Clubs[match.Away.Value].Name + " " + MinutesText(away));
            }

            return string.Join("      ", parts);
        }

        private static string MinutesText(List<int> minutes)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < minutes.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(minutes[i]);
                builder.Append('\'');
            }

            return builder.ToString();
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
