using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using Gaffer.Editor.Harness;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Editor.TransferMarket
{
    /// <summary>
    /// An early transfer bench: you have a squad, a tight budget, and a market to scout. Browse the pool
    /// through the scout mask (drag accuracy to sharpen the bands), sign a prospect on a hunch, and sell
    /// from your squad — the low-friction, tense economy in miniature (GDD §4.4). Advance a season to age
    /// and develop everyone (PlayerDevelopment): a scouted teenager grows toward his potential and his value
    /// climbs, so the whole discover-grow-sell flip is visible in one window — scout cheap, grow, sell high.
    /// Not shipped; a preview of the Faz 7 transfer UI. Reveal is a dev aid.
    /// </summary>
    public sealed class TransferMarketWindow : EditorWindow
    {
        private int _poolSize = 40;
        private int _gems = 3;
        private long _seed = 20260708L;
        private long _startingCash = 6_000_000L;
        private long _wageBudget = 160_000L;
        private float _accuracy = 0.3f;
        private bool _reveal;
        private int _season;
        private readonly PlayerDevelopment _development = new PlayerDevelopment();

        private IReadOnlyList<Player> _pool;
        private List<Player> _market;
        private Squad _squad;
        private Finances _finances;
        private string _status;
        private readonly Scout _scout = new Scout();

        private VisualElement _body;

        [MenuItem("Gaffer/Transfer Market")]
        public static void ShowWindow()
        {
            TransferMarketWindow window = GetWindow<TransferMarketWindow>();
            window.titleContent = new GUIContent("Transfer Market");
            window.minSize = new Vector2(580, 720);
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
            page.Add(Text("Scout the pool, sign on a hunch, sell to balance the books", 11, HarnessPalette.Muted));

            page.Add(BuildSetup());

            _body = new VisualElement();
            _body.style.marginTop = 6;
            page.Add(_body);
            _body.Add(Card());
            ((VisualElement)_body[0]).Add(Text("Set it up, then Generate Market.", 12, HarnessPalette.Muted));
        }

        private VisualElement BuildSetup()
        {
            VisualElement card = Card();

            var size = new IntegerField("Market size") { value = _poolSize };
            size.RegisterValueChangedCallback(e => _poolSize = e.newValue);
            card.Add(size);

            var gems = new IntegerField("Guaranteed gems") { value = _gems };
            gems.RegisterValueChangedCallback(e => _gems = e.newValue);
            card.Add(gems);

            var seed = new LongField("Seed") { value = _seed };
            seed.RegisterValueChangedCallback(e => _seed = e.newValue);
            card.Add(seed);

            var cash = new LongField("Transfer cash (€)") { value = _startingCash };
            cash.RegisterValueChangedCallback(e => _startingCash = e.newValue);
            card.Add(cash);

            var wageBudget = new LongField("Wage budget (€/wk)") { value = _wageBudget };
            wageBudget.RegisterValueChangedCallback(e => _wageBudget = e.newValue);
            card.Add(wageBudget);

            var accuracy = new Slider("Scout accuracy", 0f, 1f) { value = _accuracy };
            accuracy.RegisterValueChangedCallback(e =>
            {
                _accuracy = e.newValue;
                Render();
            });
            card.Add(accuracy);

            var reveal = new Toggle("Reveal true values (dev)") { value = _reveal };
            reveal.RegisterValueChangedCallback(e =>
            {
                _reveal = e.newValue;
                Render();
            });
            card.Add(reveal);

            var generate = new Button(Generate) { text = "Generate Market" };
            generate.style.backgroundColor = HarnessPalette.Accent;
            generate.style.color = HarnessPalette.Pitch;
            generate.style.unityFontStyleAndWeight = FontStyle.Bold;
            generate.style.height = 30;
            generate.style.marginTop = 10;
            SetRadius(generate, 6);
            card.Add(generate);

            return card;
        }

        private void Generate()
        {
            var seedRng = new SplitMix64RandomNumberGenerator((ulong)_seed);
            _squad = new SquadGenerator(new PlayerGenerator()).Generate(0, new GenerationContext(), seedRng);

            var poolGen = new PlayerPoolGenerator(new PlayerGenerator());
            GenerationContext gem = new GenerationContext
            {
                MinAge = 16, MaxAge = 19, MinAbility = 35, MaxAbility = 52, MinPotential = 84, MaxPotential = 95,
            };
            _pool = poolGen.GeneratePool(
                Mathf.Max(1, _poolSize), Mathf.Max(0, _gems), new GenerationContext(), gem,
                new SplitMix64RandomNumberGenerator((ulong)_seed ^ 0xA5A5A5UL));
            _market = new List<Player>(_pool);

            _finances = new Finances(_startingCash, _wageBudget, TotalWages(_squad));
            _season = 0;
            _status = null;
            Render();
        }

        // Ages and develops every player one season — the "grow" half of discover-grow-sell. Each player
        // develops through his own deterministic rng (seed, id, season), so a run reproduces and one player's
        // growth does not perturb another's. Wages are recomputed as abilities move.
        private void AdvanceSeason()
        {
            if (_squad == null)
            {
                return;
            }

            _season++;
            _squad = new Squad(DevelopAll(_squad.Players));
            _market = DevelopAll(_market);
            _finances = new Finances(_finances.Cash, _finances.WeeklyWageBudget, TotalWages(_squad));
            _status = "Advanced to season " + _season + " — a year of growth and decline.";
            Render();
        }

        private List<Player> DevelopAll(IReadOnlyList<Player> players)
        {
            var developed = new List<Player>(players.Count);
            foreach (Player player in players)
            {
                ulong seed = Mix((ulong)_seed, (ulong)(uint)player.Id.Value, (ulong)_season);
                developed.Add(_development.Develop(player, new SplitMix64RandomNumberGenerator(seed)));
            }

            return developed;
        }

        // SplitMix64 finalizer over the combined inputs — a cheap, well-mixed per-player season seed.
        private static ulong Mix(ulong seed, ulong id, ulong season)
        {
            ulong z = seed ^ (id * 0x9E3779B97F4A7C15UL) ^ (season * 0xD1B54A32D192ED03UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        private static long TotalWages(Squad squad)
        {
            long total = 0;
            foreach (Player player in squad.Players)
            {
                total += PlayerWage.Weekly(player);
            }

            return total;
        }

        private void Sign(Player player)
        {
            Result<TransferResult> result = TransferService.Sign(_finances, _squad, player);
            if (result.IsFailure)
            {
                _status = result.Error;
            }
            else
            {
                _finances = result.Value.Finances;
                _squad = result.Value.Squad;
                _market.Remove(player);
                _status = "Signed " + player.Name + " for " + FormatValue(result.Value.Fee) + " (" + FormatValue(PlayerWage.Weekly(player)) + "/wk).";
            }

            Render();
        }

        private void Sell(Player player)
        {
            Result<TransferResult> result = TransferService.Sell(_finances, _squad, player);
            if (result.IsFailure)
            {
                _status = result.Error;
            }
            else
            {
                _finances = result.Value.Finances;
                _squad = result.Value.Squad;
                _market.Add(player);
                _status = "Sold " + player.Name + " for " + FormatValue(result.Value.Fee) + ".";
            }

            Render();
        }

        private void Render()
        {
            _body.Clear();
            if (_squad == null)
            {
                return;
            }

            VisualElement header = Card();
            var top = new VisualElement();
            top.style.flexDirection = FlexDirection.Row;
            top.style.justifyContent = Justify.SpaceBetween;
            top.Add(Text("CASH  " + FormatValue(_finances.Cash), 15, HarnessPalette.Accent, bold: true));
            top.Add(Text("Season " + _season + " · " + _squad.Count + " in squad · accuracy " + Mathf.RoundToInt(_accuracy * 100f) + "%", 12, HarnessPalette.Muted));
            header.Add(top);

            var advance = new Button(AdvanceSeason) { text = "Advance Season (age + develop)" };
            advance.style.backgroundColor = HarnessPalette.PitchLine;
            advance.style.color = HarnessPalette.Chalk;
            advance.style.unityFontStyleAndWeight = FontStyle.Bold;
            advance.style.height = 24;
            advance.style.marginTop = 8;
            SetRadius(advance, 5);
            header.Add(advance);

            bool overWages = _finances.WageHeadroom < 0;
            Color wageColor = overWages ? HarnessPalette.Loss : HarnessPalette.Muted;
            header.Add(Text(
                "Wages " + FormatValue(_finances.WeeklyWageBill) + " / " + FormatValue(_finances.WeeklyWageBudget) +
                " per week  ·  " + FormatValue(_finances.WageHeadroom) + "/wk free  ·  " +
                FormatValue(_finances.WeeklyWageBill * 52) + "/yr",
                11, wageColor));

            if (!string.IsNullOrEmpty(_status))
            {
                header.Add(Text(_status, 11, HarnessPalette.Chalk));
            }

            _body.Add(header);
            _body.Add(BuildSquadCard());
            _body.Add(BuildMarketCard());
        }

        private VisualElement BuildSquadCard()
        {
            VisualElement card = Card();
            card.Add(Text("YOUR SQUAD", 11, HarnessPalette.Muted, bold: true));

            foreach (Player player in _squad.Players)
            {
                var row = Row();
                var line = LineWithName(player);
                var sell = new Button(() => Sell(player)) { text = "Sell " + FormatValue(TransferService.Fee(player)) };
                StyleActionButton(sell, HarnessPalette.Loss);
                line.Add(sell);
                row.Add(line);
                card.Add(row);
            }

            return card;
        }

        private VisualElement BuildMarketCard()
        {
            VisualElement card = Card();
            card.Add(Text("MARKET — " + _market.Count + " PROSPECTS", 11, HarnessPalette.Muted, bold: true));
            card.Add(Text(
                _reveal ? "Revealing true potential (dev)." : "Potential is masked — trust the band, take the punt.",
                10, HarnessPalette.Muted));

            foreach (Player player in _market)
            {
                ScoutReport report = _scout.Observe(player, _accuracy);

                var row = Row();
                var line = LineWithName(player);
                var sign = new Button(() => Sign(player)) { text = "Sign " + FormatValue(TransferService.Fee(player)) };
                StyleActionButton(sign, HarnessPalette.Accent);
                line.Add(sign);
                row.Add(line);

                string potential = "Potential " + report.PotentialLow + "–" + report.PotentialHigh;
                if (_reveal)
                {
                    potential += "   (true " + player.HiddenPotential + ")";
                }

                row.Add(Text(potential, 11, HarnessPalette.Accent));
                row.Add(Text(FormatAttributes(report), 10, HarnessPalette.Muted));
                card.Add(row);
            }

            return card;
        }

        private static VisualElement LineWithName(Player player)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;

            var name = Text(player.Name + "  ·  " + PlayerRoles.Abbrev(player.Role) + "  ·  " + player.Age, 12, HarnessPalette.Chalk, bold: true);
            name.style.flexGrow = 1;
            line.Add(name);
            var worth = Text(FormatValue(PlayerValuation.Value(player)) + " · " + FormatValue(PlayerWage.Weekly(player)) + "/wk   ", 11, HarnessPalette.Muted);
            line.Add(worth);
            return line;
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

        private static VisualElement Row()
        {
            var row = new VisualElement();
            row.style.paddingTop = 6;
            row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = HarnessPalette.PitchLine;
            return row;
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
