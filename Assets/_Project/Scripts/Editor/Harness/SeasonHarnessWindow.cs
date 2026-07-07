using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Editor.Harness
{
    /// <summary>
    /// In-editor believability workbench: runs many seasons on the pure Application core and shows the
    /// Gate A distributions in the ART_STYLE broadcast identity. Tune the balance sliders, hit Run, and
    /// read whether the sim still passes — the same check the headless tests assert, made interactive.
    /// </summary>
    public sealed class SeasonHarnessWindow : EditorWindow
    {
        private int _seasons = 500;
        private long _seed = 20260707L;
        private int _teamCount = 20;
        private float _baseChances = (float)MatchSimulationSettings.Default.BaseChancesPerTeam;
        private float _meanQuality = (float)MatchSimulationSettings.Default.MeanChanceQuality;
        private float _homeAdvantage = (float)MatchSimulationSettings.Default.HomeAdvantage;
        private float _maxRatio = (float)MatchSimulationSettings.Default.MaxStrengthRatio;

        private VisualElement _resultsRoot;
        private Button _exportButton;
        private HarnessReport _report;

        [MenuItem("Gaffer/Season Harness")]
        public static void ShowWindow()
        {
            SeasonHarnessWindow window = GetWindow<SeasonHarnessWindow>();
            window.titleContent = new GUIContent("Season Harness");
            window.minSize = new Vector2(540, 660);
        }

        public void CreateGUI()
        {
            var scroll = new ScrollView();
            scroll.style.backgroundColor = HarnessPalette.Pitch;
            rootVisualElement.Add(scroll);

            var page = new VisualElement();
            SetPadding(page, 20);
            scroll.Add(page);

            var title = MakeLabel("SEASON HARNESS", 22, HarnessPalette.Chalk, bold: true);
            title.style.letterSpacing = 2f;
            page.Add(title);
            page.Add(MakeLabel("Believability workbench / pure simulation core", 11, HarnessPalette.Muted));

            page.Add(BuildControls());

            _resultsRoot = new VisualElement();
            _resultsRoot.style.marginTop = 6;
            page.Add(_resultsRoot);

            ShowHint();
        }

        private VisualElement BuildControls()
        {
            VisualElement card = MakeCard();

            var seasons = new IntegerField("Seasons") { value = _seasons };
            seasons.RegisterValueChangedCallback(e => _seasons = e.newValue);
            card.Add(seasons);

            var teams = new IntegerField("Teams") { value = _teamCount };
            teams.RegisterValueChangedCallback(e => _teamCount = e.newValue);
            card.Add(teams);

            var seed = new LongField("Seed") { value = _seed };
            seed.RegisterValueChangedCallback(e => _seed = e.newValue);
            card.Add(seed);

            card.Add(MakeSlider("Base chances / team", 3f, 12f, _baseChances, v => _baseChances = v));
            card.Add(MakeSlider("Mean chance quality", 0.05f, 0.35f, _meanQuality, v => _meanQuality = v));
            card.Add(MakeSlider("Home advantage", 1.0f, 1.4f, _homeAdvantage, v => _homeAdvantage = v));
            card.Add(MakeSlider("Max strength ratio", 1.2f, 3.0f, _maxRatio, v => _maxRatio = v));

            var run = new Button(RunHarness) { text = "Run" };
            run.style.backgroundColor = HarnessPalette.Accent;
            run.style.color = HarnessPalette.Pitch;
            run.style.unityFontStyleAndWeight = FontStyle.Bold;
            run.style.height = 30;
            run.style.marginTop = 10;
            SetBorder(run, HarnessPalette.Accent, 0);
            SetRadius(run, 6);
            card.Add(run);

            _exportButton = new Button(ExportHtml) { text = "Export HTML report" };
            _exportButton.style.marginTop = 4;
            _exportButton.SetEnabled(false);
            card.Add(_exportButton);

            return card;
        }

        private Slider MakeSlider(string label, float min, float max, float value, System.Action<float> onChange)
        {
            var slider = new Slider(label, min, max) { value = value, showInputField = true };
            slider.RegisterValueChangedCallback(e => onChange(e.newValue));
            return slider;
        }

        private void RunHarness()
        {
            var config = new HarnessConfig
            {
                SeasonCount = Mathf.Max(1, _seasons),
                TeamCount = Mathf.Clamp(_teamCount, 2, 24),
                Seed = (ulong)_seed,
            };
            var settings = new MatchSimulationSettings(_baseChances, _meanQuality, _homeAdvantage, _maxRatio);

            var rng = new SplitMix64RandomNumberGenerator(config.Seed);
            IReadOnlyList<TeamProfile> teams = new LeagueFactory().CreateTeams(config, rng);
            var simulator = new MatchSimulator(new PoissonChanceGenerator(settings), new QualityChanceResolver());
            var runner = new SeasonRunner(simulator);
            var statistics = new HarnessStatistics(config.TeamCount);

            for (int season = 0; season < config.SeasonCount; season++)
            {
                runner.RunSeason(teams, rng, statistics);
            }

            _report = statistics.BuildReport(config, teams);
            _exportButton.SetEnabled(true);
            BuildResults();
        }

        private void ExportHtml()
        {
            if (_report == null)
            {
                return;
            }

            string path = EditorUtility.SaveFilePanel("Export season report", string.Empty, "season-report.html", "html");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            File.WriteAllText(path, new HtmlReportWriter().Render(_report));
            EditorUtility.RevealInFinder(path);
        }

        private void ShowHint()
        {
            _resultsRoot.Clear();
            VisualElement card = MakeCard();
            card.Add(MakeLabel("Set the balance, then Run to simulate the seasons.", 12, HarnessPalette.Muted));
            _resultsRoot.Add(card);
        }

        private void BuildResults()
        {
            _resultsRoot.Clear();

            var chips = new VisualElement();
            chips.style.flexDirection = FlexDirection.Row;
            chips.style.flexWrap = Wrap.Wrap;
            chips.style.marginTop = 8;
            foreach (GateCheck check in _report.GateChecks)
            {
                chips.Add(MakeGateChip(check));
            }
            _resultsRoot.Add(chips);

            VisualElement summary = MakeCard();
            summary.Add(MakeLabel(
                "Home " + Fmt(_report.HomeWinPercentage) + "%   Draw " + Fmt(_report.DrawPercentage) +
                "%   Away " + Fmt(_report.AwayWinPercentage) + "%", 13, HarnessPalette.Chalk));
            summary.Add(MakeLabel(
                "Favourite: win " + Fmt(_report.FavouriteWinPercentage) + "%   draw " +
                Fmt(_report.FavouriteDrawPercentage) + "%   upset " + Fmt(_report.FavouriteUpsetPercentage) + "%",
                12, HarnessPalette.Muted));
            _resultsRoot.Add(summary);

            var goalBars = new List<BarData>();
            foreach (HistogramBin bin in _report.GoalBins)
            {
                goalBars.Add(new BarData(bin.Label, bin.Percentage, bin.Percentage >= 1.0 ? Fmt0(bin.Percentage) : string.Empty));
            }
            _resultsRoot.Add(MakeChartCard("Goals per match  (avg " + Fmt2(_report.AverageGoalsPerMatch) + ")", goalBars));

            var titleBars = new List<BarData>();
            foreach (ChampionShare share in _report.ChampionShares)
            {
                titleBars.Add(new BarData((share.Rank + 1).ToString(), share.Percentage, share.Percentage >= 3.0 ? Fmt0(share.Percentage) : string.Empty));
            }
            _resultsRoot.Add(MakeChartCard("Titles by pre-season seed (1 = strongest)", titleBars));

            _resultsRoot.Add(MakeTableCard());
        }

        private VisualElement MakeGateChip(GateCheck check)
        {
            Color status = check.Status == GateStatus.Pass ? HarnessPalette.Win
                : check.Status == GateStatus.Warn ? HarnessPalette.Draw : HarnessPalette.Loss;

            VisualElement chip = MakeCard();
            chip.style.borderLeftWidth = 3;
            chip.style.borderLeftColor = status;
            chip.style.flexGrow = 1;
            chip.style.flexBasis = 150;
            chip.style.marginRight = 8;

            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.Add(MakeLabel(check.Label.ToUpperInvariant(), 9, HarnessPalette.Muted));
            top.Add(MakeLabel(TagText(check.Status), 9, status, bold: true));
            chip.Add(top);

            chip.Add(MakeLabel(check.Value, 18, HarnessPalette.Chalk, bold: true));
            chip.Add(MakeLabel(check.Detail, 10, HarnessPalette.Muted));
            return chip;
        }

        private VisualElement MakeChartCard(string title, IReadOnlyList<BarData> bars)
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel(title.ToUpperInvariant(), 11, HarnessPalette.Muted, bold: true));

            var chart = new VisualElement();
            chart.style.flexDirection = FlexDirection.Row;
            chart.style.alignItems = Align.FlexEnd;
            chart.style.height = 150;
            chart.style.marginTop = 10;

            double max = 1.0;
            foreach (BarData bar in bars)
            {
                if (bar.Value > max)
                {
                    max = bar.Value;
                }
            }

            foreach (BarData bar in bars)
            {
                var column = new VisualElement();
                column.style.flexGrow = 1;
                column.style.flexBasis = 0;
                column.style.alignItems = Align.Center;
                column.style.justifyContent = Justify.FlexEnd;
                column.style.marginLeft = 2;
                column.style.marginRight = 2;

                column.Add(MakeLabel(bar.ValueText, 9, HarnessPalette.Chalk));

                var fill = new VisualElement();
                float height = (float)(110.0 * bar.Value / max);
                if (bar.Value > 0.0 && height < 2f)
                {
                    height = 2f;
                }
                fill.style.height = height;
                fill.style.width = Length.Percent(68);
                fill.style.backgroundColor = HarnessPalette.Accent;
                fill.style.borderTopLeftRadius = 2;
                fill.style.borderTopRightRadius = 2;
                column.Add(fill);

                column.Add(MakeLabel(bar.Label, 9, HarnessPalette.Muted));
                chart.Add(column);
            }

            card.Add(chart);
            return card;
        }

        private VisualElement MakeTableCard()
        {
            VisualElement card = MakeCard();
            card.Add(MakeLabel("SAMPLE FINAL TABLE", 11, HarnessPalette.Muted, bold: true));

            string[] heads = { "#", "Club", "P", "W", "D", "L", "GF", "GA", "GD", "Pts" };
            card.Add(MakeTableRow(heads, HarnessPalette.Muted, bold: true, background: new Color(0, 0, 0, 0)));

            int relegationFrom = _report.SampleTable.Count - 3;
            foreach (TableRowView row in _report.SampleTable)
            {
                Color background = row.Position == 1 ? Tint(HarnessPalette.Accent, 0.12f)
                    : row.Position > relegationFrom ? Tint(HarnessPalette.Loss, 0.08f)
                    : new Color(0, 0, 0, 0);

                string[] cells =
                {
                    row.Position.ToString(), row.Name, row.Played.ToString(), row.Won.ToString(),
                    row.Drawn.ToString(), row.Lost.ToString(), row.GoalsFor.ToString(), row.GoalsAgainst.ToString(),
                    Signed(row.GoalDifference), row.Points.ToString(),
                };
                card.Add(MakeTableRow(cells, HarnessPalette.Chalk, bold: false, background: background));
            }

            return card;
        }

        private VisualElement MakeTableRow(IReadOnlyList<string> cells, Color color, bool bold, Color background)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            row.style.backgroundColor = background;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = HarnessPalette.PitchLine;

            float[] widths = { 26, -1, 28, 28, 28, 28, 32, 32, 34, 36 };
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

        private static string TagText(GateStatus status)
        {
            return status == GateStatus.Pass ? "PASS" : status == GateStatus.Warn ? "WARN" : "FAIL";
        }

        private static string Signed(int value)
        {
            return value > 0 ? "+" + value.ToString(CultureInfo.InvariantCulture) : value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Fmt(double value)
        {
            return value.ToString("F1", CultureInfo.InvariantCulture);
        }

        private static string Fmt0(double value)
        {
            return value.ToString("F0", CultureInfo.InvariantCulture);
        }

        private static string Fmt2(double value)
        {
            return value.ToString("F2", CultureInfo.InvariantCulture);
        }

        private sealed class BarData
        {
            public BarData(string label, double value, string valueText)
            {
                Label = label;
                Value = value;
                ValueText = valueText;
            }

            public string Label { get; }

            public double Value { get; }

            public string ValueText { get; }
        }
    }
}
