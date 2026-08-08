using System;
using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using Gaffer.Editor.Balance;
using Gaffer.Editor.Harness;
using Gaffer.Infrastructure.Configuration;
using UnityEditor;
using UnityEditor.UIElements;
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
        private PlayerDevelopment _development = new PlayerDevelopment();
        private DevelopmentBalanceSO _balance;

        private IReadOnlyList<Player> _pool;
        private List<Player> _market;
        private Squad _squad;
        private Finances _finances;
        private string _status;
        private readonly Scout _scout = new Scout();

        // The market strongest-first, and the squad strongest-first. Kept as fields and refilled only when
        // the roster behind them actually changes, so a render never re-sorts and never re-rates: the
        // ListView reads _marketOrder straight through.
        private readonly List<Player> _marketOrder = new List<Player>();
        private readonly List<Player> _squadOrder = new List<Player>();

        private VisualElement _body;

        // Live handles into the body, for the repaints that must not rebuild it (see RebindMarket). Every
        // one of these is dropped at the top of Render, because Render clears the hierarchy they live in.
        private ListView _marketList;
        private Label _marketMask;
        private Label _headerLine;

        [MenuItem("Gaffer/Transfer Market")]
        public static void ShowWindow()
        {
            TransferMarketWindow window = GetWindow<TransferMarketWindow>();
            window.titleContent = new GUIContent("Transfer Market");
            window.minSize = new Vector2(580, 720);
        }

        public void CreateGUI()
        {
            // Pre-assign the default development-balance asset (created on first use), so the field is filled.
            _balance = _balance != null ? _balance : BalanceAssets.Development();

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

            // Dragging this fires the callback every frame. It used to call Render(), which cleared the
            // body and rebuilt a row for every prospect in the pool — the whole market, per frame. It now
            // rebinds instead: only the rows the ListView has actually realized are re-scouted.
            var accuracy = new Slider("Scout accuracy", 0f, 1f) { value = _accuracy };
            accuracy.RegisterValueChangedCallback(e =>
            {
                _accuracy = e.newValue;
                RebindMarket();
            });
            card.Add(accuracy);

            var reveal = new Toggle("Reveal true values (dev)") { value = _reveal };
            reveal.RegisterValueChangedCallback(e =>
            {
                _reveal = e.newValue;
                RebindMarket();
            });
            card.Add(reveal);

            // Optional development-balance asset: assign a DevelopmentBalanceSO to retune how players grow and
            // age (used by Advance Season). Leave empty to use the calibrated defaults.
            var balance = new ObjectField("Development balance (SO)") { objectType = typeof(DevelopmentBalanceSO), value = _balance };
            balance.RegisterValueChangedCallback(e => _balance = e.newValue as DevelopmentBalanceSO);
            card.Add(balance);

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
            Reorder();
            Render();
        }

        // Refills both strongest-first orders. Called only where the rosters actually change — generate,
        // advance a season, sign, sell — never from Render, so scrolling and scouting cost nothing.
        private void Reorder()
        {
            ByOverallDescending(_market, _marketOrder);
            ByOverallDescending(_squad.Players, _squadOrder);
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

            // Rebuild the developer from the assigned balance asset (or the calibrated default), so tuning the
            // SO in the Inspector changes how this season's growth and decline play out.
            _development = _balance != null ? new PlayerDevelopment(_balance.ToSettings()) : new PlayerDevelopment();

            _season++;
            _squad = new Squad(DevelopAll(_squad.Players));
            _market = DevelopAll(_market);
            _finances = new Finances(_finances.Cash, _finances.WeeklyWageBudget, TotalWages(_squad));
            _status = "Advanced to season " + _season + " — a year of growth and decline.";
            Reorder();
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
                Reorder();
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
                Reorder();
            }

            Render();
        }

        // A full rebuild of the body. Reserved for the things that actually change the rosters or the
        // books — generate, advance, sign, sell. Scouting accuracy and reveal go through RebindMarket.
        private void Render()
        {
            // The body is about to be cleared, so the handles into it go first: a ListView left in a field
            // after its hierarchy was dropped would be rebound into nothing (UNITY.md §5).
            _marketList = null;
            _marketMask = null;
            _headerLine = null;

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
            _headerLine = Text(HeaderLine(), 12, HarnessPalette.Muted);
            top.Add(_headerLine);
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

        private string HeaderLine()
        {
            return "Season " + _season + " · " + _squad.Count + " in squad · accuracy " +
                Mathf.RoundToInt(_accuracy * 100f) + "%";
        }

        private string MaskNote()
        {
            return _reveal
                ? "Revealing true potential (dev)."
                : "OVR is his current ability (visible); potential is masked — trust the band, take the punt.";
        }

        // Repaints exactly what a scouting-accuracy or reveal change alters: the visible market rows and
        // the two lines of copy that quote the setting. Nothing is created or destroyed, so this is what
        // the accuracy slider can afford to run on every frame of a drag. No-ops before the first Render.
        private void RebindMarket()
        {
            if (_headerLine != null)
            {
                _headerLine.text = HeaderLine();
            }

            if (_marketMask != null)
            {
                _marketMask.text = MaskNote();
            }

            if (_marketList != null)
            {
                _marketList.RefreshItems();
            }
        }

        private VisualElement BuildSquadCard()
        {
            VisualElement card = Card();
            card.Add(Text("YOUR SQUAD", 11, HarnessPalette.Muted, bold: true));

            card.Add(Text("Your own players — true overall (OVR) and full attributes. Advance a season to watch the young ones grow.", 10, HarnessPalette.Muted));

            foreach (Player player in _squadOrder)
            {
                var row = Row();
                var line = LineWithName(player, showRating: true);
                var sell = new Button(() => Sell(player)) { text = "Sell " + FormatValue(TransferService.Fee(player)) };
                StyleActionButton(sell, HarnessPalette.Loss);
                line.Add(sell);
                row.Add(line);
                row.Add(Text(AllAttributesText(player), 10, HarnessPalette.Muted));
                card.Add(row);
            }

            return card;
        }

        // ----- The market list ------------------------------------------------------------------------

        // The row draws a fixed four lines, so the ListView can virtualize by height: it creates only as
        // many rows as fit the viewport and rebinds them as you scroll. Nothing here scales with the pool.
        private const float MarketRowHeight = 78f;
        private const float MarketListHeight = 460f;

        private VisualElement BuildMarketCard()
        {
            VisualElement card = Card();
            card.Add(Text("MARKET — " + _marketOrder.Count + " PROSPECTS", 11, HarnessPalette.Muted, bold: true));
            _marketMask = Text(MaskNote(), 10, HarnessPalette.Muted);
            card.Add(_marketMask);
            card.Add(Text(
                "The fee and the weekly wage are both on the button: a signing has to clear the cash AND the wage room.",
                10, HarnessPalette.Muted));

            // Virtualized. The card used to build five to eight VisualElements per prospect for the whole
            // market — 300,000 of them at a 50,000-player pool — and scout every one of them on the way.
            // The ListView holds about seven live rows regardless, and only those get Observe'd (bindItem).
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
                itemsSource = _marketOrder,
            };
            _marketList.style.height = MarketListHeight;
            _marketList.style.marginTop = 6;
            card.Add(_marketList);

            return card;
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
            if (index < 0 || index >= _marketOrder.Count)
            {
                view.Unbind();
                return;
            }

            Player player = _marketOrder[index];
            view.Bind(
                player,
                _scout.Observe(player, _accuracy),
                SigningVerdict.For(TransferService.Fee(player), PlayerWage.Weekly(player), _finances),
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
            private readonly TransferMarketWindow _window;
            private readonly Label _name;
            private readonly Label _rating;
            private readonly Label _worth;
            private readonly Button _sign;
            private readonly Label _books;
            private readonly Label _potential;
            private readonly Label _attributes;

            // Who this row is showing right now. The row's only piece of per-bind state, and the only
            // thing the button reads.
            private Player _player;

            internal MarketRowView(TransferMarketWindow window)
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

                _name = Text(string.Empty, 12, HarnessPalette.Chalk, bold: true);
                _name.style.flexGrow = 1;
                _name.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(_name);

                _rating = Text(string.Empty, 12, HarnessPalette.Accent, bold: true);
                _rating.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(_rating);

                _worth = Text(string.Empty, 11, HarnessPalette.Muted);
                _worth.style.whiteSpace = WhiteSpace.NoWrap;
                line.Add(_worth);

                _sign = new Button(SignCurrent);
                StyleActionButton(_sign, HarnessPalette.Accent);
                line.Add(_sign);
                Root.Add(line);

                _books = Text(string.Empty, 10, HarnessPalette.Muted);
                _books.style.whiteSpace = WhiteSpace.NoWrap;
                Root.Add(_books);

                _potential = Text(string.Empty, 11, HarnessPalette.Accent);
                _potential.style.whiteSpace = WhiteSpace.NoWrap;
                Root.Add(_potential);

                _attributes = Text(string.Empty, 10, HarnessPalette.Muted);
                _attributes.style.whiteSpace = WhiteSpace.NoWrap;
                Root.Add(_attributes);
            }

            internal VisualElement Root { get; }

            /// <summary>
            /// Repaints the row for a different player. Every mutable thing the row draws — and the player
            /// the button will act on — is assigned here unconditionally: no "only if it changed" branch,
            /// no field left holding the previous occupant. A rebind that forgets one of these is how a
            /// recycled row signs the wrong man, so the reset is total rather than incremental.
            /// </summary>
            internal void Bind(Player player, ScoutReport report, SigningVerdict verdict, bool reveal)
            {
                _player = player;

                _name.text = player.Name + "  ·  " + HarnessLabels.RoleLabel(player.Role) + "  ·  " + player.Age;
                _rating.text = "OVR " + Mathf.RoundToInt((float)PlayerRatings.ForRole(player)) + "   ";
                _worth.text = FormatValue(PlayerValuation.Value(player)) + " worth   ";

                _sign.text = verdict.ActionLabel();
                _sign.SetEnabled(true);
                _sign.style.backgroundColor = verdict.Affordable ? HarnessPalette.Accent : HarnessPalette.PitchLine;
                _sign.style.color = verdict.Affordable ? HarnessPalette.Pitch : HarnessPalette.Muted;

                // The blocked reason is shown, not enforced: the button stays live so TransferService.Sign
                // still gets the click and still writes the authoritative message. The row only says in
                // advance what that message would be.
                _books.text = verdict.Sentence();
                _books.style.color = verdict.Tone;
                Root.style.borderLeftWidth = verdict.Affordable ? 0 : 2;

                _potential.text = reveal
                    ? "Potential " + report.PotentialLow + "–" + report.PotentialHigh + "   (true " + player.HiddenPotential + ")"
                    : "Potential " + report.PotentialLow + "–" + report.PotentialHigh;
                _attributes.text = FormatAttributes(report);
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

        // Strongest first, by current overall rating — the natural way to read a squad or a shortlist. Ties
        // break on the lower player id so the order is stable across renders.
        //
        // Each player is rated exactly once, into an array, and the sort compares array entries. The
        // comparator used to call PlayerRatings.ForRole twice per comparison, which is ~2·n·log n rating
        // evaluations — about 1.5 million for a 50,000-player pool, for 50,000 players' worth of data.
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

        private static VisualElement LineWithName(Player player, bool showRating)
        {
            var line = new VisualElement();
            line.style.flexDirection = FlexDirection.Row;
            line.style.alignItems = Align.Center;

            var name = Text(player.Name + "  ·  " + HarnessLabels.RoleLabel(player.Role) + "  ·  " + player.Age, 12, HarnessPalette.Chalk, bold: true);
            name.style.flexGrow = 1;
            line.Add(name);
            if (showRating)
            {
                // General rating for the role (current ability) — the number that moves as a player
                // develops. Shown for both your squad and the market; only potential stays scout-masked.
                int rating = Mathf.RoundToInt((float)PlayerRatings.ForRole(player));
                line.Add(Text("OVR " + rating + "   ", 12, HarnessPalette.Accent, bold: true));
            }

            var worth = Text(FormatValue(PlayerValuation.Value(player)) + " · " + FormatValue(PlayerWage.Weekly(player)) + "/wk   ", 11, HarnessPalette.Muted);
            line.Add(worth);
            return line;
        }

        // Every attribute, grouped, for a player you own — full transparency (the scout mask only applies to
        // the market). The goalkeeping block is shown only for keepers, where it is the meaningful group.
        private static string AllAttributesText(Player player)
        {
            Attributes a = player.Attributes;
            string technical =
                "TEC  FIN " + a.Finishing + "  TEC " + a.Technique + "  FIR " + a.FirstTouch + "  DRI " + a.Dribbling +
                "  PAS " + a.Passing + "  CRO " + a.Crossing + "  HEA " + a.Heading + "  LON " + a.LongShots +
                "  MAR " + a.Marking + "  TKL " + a.Tackling;
            string setPiece =
                "SET  PEN " + a.Penalties + "  FRK " + a.FreeKicks + "  COR " + a.Corners + "  THR " + a.LongThrows;
            string physical =
                "PHY  PAC " + a.Pace + "  ACC " + a.Acceleration + "  STA " + a.Stamina + "  STR " + a.Strength +
                "  AGI " + a.Agility + "  JMP " + a.Jumping + "  BAL " + a.Balance + "  POS " + a.Positioning;
            string text = technical + "\n" + setPiece + "\n" + physical;

            if (player.Role == PlayerRole.Goalkeeper)
            {
                text += "\nGK   REF " + a.Reflexes + "  HAN " + a.Handling + "  AER " + a.AerialReach +
                    "  CMD " + a.CommandOfArea + "  1v1 " + a.OneOnOnes + "  KIC " + a.Kicking + "  GKP " + a.GkPositioning;
            }

            return text;
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
                parts.Add(HarnessLabels.LabelForKey(estimate.LabelKey) + " " + band);
            }

            return string.Join("   ", parts);
        }

        // One implementation, shared with the affordability copy, so a fee on a button and the shortfall
        // under it can never be written two different ways (and a negative is signed, not "€-500000").
        private static string FormatValue(long value)
        {
            return HarnessMoney.Format(value);
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
