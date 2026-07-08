using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Players;
using Gaffer.Editor.Harness;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Position = Gaffer.Domain.Players.Position;

namespace Gaffer.Editor.TransferMarket
{
    /// <summary>
    /// An early scouting bench: generate a run's player pool (guaranteed wonderkid included) and browse it
    /// through the scout mask. Drag the accuracy slider and watch the attribute and potential bands narrow —
    /// the discovery fantasy in miniature: at low accuracy a gem hides among ordinary prospects. Not shipped;
    /// a preview of how the real transfer UI (Faz 7) will present scouting. Reveal is a dev aid only.
    /// </summary>
    public sealed class TransferMarketWindow : EditorWindow
    {
        private int _poolSize = 60;
        private int _gems = 3;
        private long _seed = 20260708L;
        private float _accuracy = 0.3f;
        private bool _reveal;

        private IReadOnlyList<Player> _pool;
        private readonly Scout _scout = new Scout();

        private VisualElement _list;

        [MenuItem("Gaffer/Transfer Market")]
        public static void ShowWindow()
        {
            TransferMarketWindow window = GetWindow<TransferMarketWindow>();
            window.titleContent = new GUIContent("Transfer Market");
            window.minSize = new Vector2(560, 680);
        }

        public void CreateGUI()
        {
            var scroll = new ScrollView();
            scroll.style.backgroundColor = HarnessPalette.Pitch;
            rootVisualElement.Add(scroll);

            var page = new VisualElement();
            SetPadding(page, 20);
            scroll.Add(page);

            Label title = Text("TRANSFER MARKET", 22, HarnessPalette.Chalk, bold: true);
            title.style.letterSpacing = 2f;
            page.Add(title);
            page.Add(Text("Scout the pool — drag accuracy to sharpen the bands", 11, HarnessPalette.Muted));

            page.Add(BuildSetup());

            _list = new VisualElement();
            _list.style.marginTop = 6;
            page.Add(_list);
            _list.Add(Card());
            ((VisualElement)_list[0]).Add(Text("Set it up, then Generate Pool.", 12, HarnessPalette.Muted));
        }

        private VisualElement BuildSetup()
        {
            VisualElement card = Card();

            var size = new IntegerField("Pool size") { value = _poolSize };
            size.RegisterValueChangedCallback(e => _poolSize = e.newValue);
            card.Add(size);

            var gems = new IntegerField("Guaranteed gems") { value = _gems };
            gems.RegisterValueChangedCallback(e => _gems = e.newValue);
            card.Add(gems);

            var seed = new LongField("Seed") { value = _seed };
            seed.RegisterValueChangedCallback(e => _seed = e.newValue);
            card.Add(seed);

            var accuracy = new Slider("Scout accuracy", 0f, 1f) { value = _accuracy };
            accuracy.RegisterValueChangedCallback(e =>
            {
                _accuracy = e.newValue;
                RenderList();
            });
            card.Add(accuracy);

            var reveal = new Toggle("Reveal true values (dev)") { value = _reveal };
            reveal.RegisterValueChangedCallback(e =>
            {
                _reveal = e.newValue;
                RenderList();
            });
            card.Add(reveal);

            var generate = new Button(GeneratePool) { text = "Generate Pool" };
            generate.style.backgroundColor = HarnessPalette.Accent;
            generate.style.color = HarnessPalette.Pitch;
            generate.style.unityFontStyleAndWeight = FontStyle.Bold;
            generate.style.height = 30;
            generate.style.marginTop = 10;
            SetRadius(generate, 6);
            card.Add(generate);

            return card;
        }

        private void GeneratePool()
        {
            var generator = new PlayerPoolGenerator(new PlayerGenerator());
            GenerationContext ordinary = new GenerationContext();
            GenerationContext gem = new GenerationContext
            {
                MinAge = 16, MaxAge = 19, MinAbility = 35, MaxAbility = 52, MinPotential = 84, MaxPotential = 95,
            };

            _pool = generator.GeneratePool(
                Mathf.Max(1, _poolSize), Mathf.Max(0, _gems), ordinary, gem,
                new SplitMix64RandomNumberGenerator((ulong)_seed));
            RenderList();
        }

        private void RenderList()
        {
            _list.Clear();
            if (_pool == null)
            {
                return;
            }

            VisualElement header = Card();
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.Add(Text(_pool.Count + " PROSPECTS", 13, HarnessPalette.Accent, bold: true));
            top.Add(Text("Accuracy " + Mathf.RoundToInt(_accuracy * 100f) + "%", 12, HarnessPalette.Muted));
            header.Add(top);
            header.Add(Text(
                _reveal ? "Revealing true potential (dev)." : "Potential is masked — trust the band, take the punt.",
                11, HarnessPalette.Muted));
            _list.Add(header);

            VisualElement card = Card();
            for (int i = 0; i < _pool.Count; i++)
            {
                card.Add(BuildRow(_pool[i]));
            }

            _list.Add(card);
        }

        private VisualElement BuildRow(Player player)
        {
            ScoutReport report = _scout.Observe(player, _accuracy);

            var row = new VisualElement();
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = HarnessPalette.PitchLine;

            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.justifyContent = Justify.SpaceBetween;

            var left = Text(player.Name + "  ·  " + Abbrev(player.Position) + "  ·  " + player.Age, 12, HarnessPalette.Chalk, bold: true);
            left.style.flexGrow = 1;
            line.Add(left);
            line.Add(Text(FormatValue(PlayerValuation.Value(player)), 12, HarnessPalette.Chalk, bold: true));
            row.Add(line);

            string potential = "Potential " + report.PotentialLow + "–" + report.PotentialHigh;
            if (_reveal)
            {
                potential += "   (true " + player.HiddenPotential + ")";
            }

            row.Add(Text(potential, 11, HarnessPalette.Accent));
            row.Add(Text(FormatAttributes(report), 10, HarnessPalette.Muted));

            return row;
        }

        private static string FormatAttributes(ScoutReport report)
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

        private static string FormatValue(long value)
        {
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

        private static string Abbrev(Position position)
        {
            switch (position)
            {
                case Position.Goalkeeper:
                    return "GK";
                case Position.Defender:
                    return "DEF";
                case Position.Midfielder:
                    return "MID";
                default:
                    return "FWD";
            }
        }

        private static Label Text(string text, int size, Color color, bool bold = false)
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

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.style.backgroundColor = HarnessPalette.PitchRaised;
            card.style.borderTopWidth = 1;
            card.style.borderBottomWidth = 1;
            card.style.borderLeftWidth = 1;
            card.style.borderRightWidth = 1;
            card.style.borderTopColor = HarnessPalette.PitchLine;
            card.style.borderBottomColor = HarnessPalette.PitchLine;
            card.style.borderLeftColor = HarnessPalette.PitchLine;
            card.style.borderRightColor = HarnessPalette.PitchLine;
            SetRadius(card, 10);
            SetPadding(card, 14);
            card.style.marginTop = 8;
            return card;
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
    }
}
