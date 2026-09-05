using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common.Localization;
using Gaffer.Domain.Players;
using Gaffer.Presentation.Shell;
using Gaffer.Presentation.Squad;
using UnityEngine.UIElements;

// The pitch position, not the layout one: UIElements has a Position of its own.
using Position = Gaffer.Domain.Players.Position;

namespace Gaffer.Presentation.Market
{
    /// <summary>
    /// The transfer market — the fourth of the five screens (GDD §8: pool, offers, scout reports). Who is
    /// for sale, whether the window is open, what the club can spend; and the same for the club's own men.
    ///
    /// <para><b>Three questions, in the order a manager asks them.</b> WHEN: the status line says whether
    /// a deal can be done today and, if not, when it can. WHAT: a list of tiles, best first, each one a
    /// name, a number and a price. WHY NOT: a row the club cannot afford is dimmed before it is tapped, and
    /// the card behind it names the shortfall in money before the button is pressed — the playtest lesson
    /// "I have cash but I can't sign him" made into a rule.</para>
    ///
    /// <para><b>Render-time queries only.</b> Everything here is read off the session when the tab is
    /// shown or a sheet closes; the screen never works out what changed (NON-NEGOTIABLE #4). Which
    /// players a filter leaves and how they are ordered is <see cref="MarketList"/>'s job, held by
    /// <c>dotnet test</c>; what a signing will do to the money is <see cref="Affordability"/>'s.</para>
    ///
    /// <para><b>A ListView, because the pool is two thousand and may be fifty.</b> The squad's twenty rows
    /// are plain elements; this list recycles ten. Each row is a tap target of the row's full height, with
    /// its labels picking-disabled, so a tap on a name is a tap on the row.</para>
    /// </summary>
    public sealed class MarketScreen
    {
        private const float RowHeight = 120f;

        private readonly RunSession _session;
        private readonly VisualElement _root;
        private readonly IScreenHost _host;
        private readonly LocalizedStrings _text;

        private readonly Label _window = new Label();
        private readonly Label _opens = new Label();
        private readonly Label _cash = new Label();
        private readonly Label _wageRoom = new Label();

        private readonly Button _segmentMarket = new Button();
        private readonly Button _segmentSquad = new Button();
        private readonly List<Button> _lineChips = new List<Button>(5);
        private readonly Button _affordableChip = new Button();

        // The pool as shown: sorted once per refresh, then the slice the filters leave. Held and refilled
        // rather than replaced so the ListView keeps one source (PERFORMANCE §8).
        private readonly List<Player> _source = new List<Player>();
        private readonly List<Player> _shown = new List<Player>();
        private readonly ListView _list;
        private readonly Label _empty = new Label();

        private bool _showingSquad;
        private Position? _line;
        private bool _affordableOnly;

        public MarketScreen(RunSession session, VisualElement root, IScreenHost host, LocalizedStrings text)
        {
            _session = session;
            _root = root;
            _host = host;
            _text = text;
            _list = new ListView
            {
                itemsSource = _shown,
                fixedItemHeight = RowHeight,
                virtualizationMethod = CollectionVirtualizationMethod.FixedHeight,
                selectionType = SelectionType.None,
                makeItem = MakeRow,
                bindItem = BindRow,
            };
        }

        public void Build()
        {
            _root.Clear();
            _root.Add(BuildStatus());
            _root.Add(BuildSegment());
            _root.Add(BuildFilters());
            _root.Add(BuildList());

            // The starting state, stated: the market segment and the ALL chip are lit before anything is
            // tapped, so the controls read as controls and not as headings.
            _segmentMarket.AddToClassList("segment__button--active");
            _lineChips[0].AddToClassList("chip--active");
            Refresh();
        }

        /// <summary>Redraws from the run: the money, the window, and the list. Called by the shell when the
        /// tab is shown and when a sheet closes over it — a signing changed all three.</summary>
        public void Refresh()
        {
            DrawStatus();
            Reload();
        }

        // ----- When -----------------------------------------------------------------------------------------

        private VisualElement BuildStatus()
        {
            VisualElement card = Card();

            _window.AddToClassList("label");
            _window.AddToClassList("market__window");
            card.Add(_window);

            _opens.AddToClassList("market__opens");
            card.Add(_opens);

            var money = new VisualElement();
            money.AddToClassList("kv-row");
            money.Add(Kv(UiTextKeys.MarketCash, _cash));
            money.Add(Kv(UiTextKeys.MarketWageRoom, _wageRoom));
            card.Add(money);

            return card;
        }

        private void DrawStatus()
        {
            TransferWindowPhase phase = _session.WindowPhase;
            bool open = phase != TransferWindowPhase.Closed;

            _window.text = _text.Or(
                phase == TransferWindowPhase.Summer ? UiTextKeys.MarketWindowSummer
                : phase == TransferWindowPhase.Winter ? UiTextKeys.MarketWindowWinter
                : UiTextKeys.MarketWindowClosed);
            _window.EnableInClassList("market__window--open", open);

            // Said only when it needs saying. A closed market that did not say when it reopens would leave
            // the manager pressing "play the week" to find out.
            _opens.style.display = open ? DisplayStyle.None : DisplayStyle.Flex;
            if (!open)
            {
                int? reopens = TransferWindow.NextOpening(_session.PlayedRounds, _session.RoundCount);
                _opens.text = reopens.HasValue
                    ? _text.Or(UiTextKeys.MarketOpensAfter, Count(reopens.Value))
                    : _text.Or(UiTextKeys.MarketOpensSummer);
            }

            Finances finances = _session.Finances;
            _cash.text = _text.Money(finances.Cash);
            _wageRoom.text = _text.Money(finances.WageHeadroom) + _text.Or(UiTextKeys.MarketPerWeek);
        }

        // ----- Which list -----------------------------------------------------------------------------------

        private VisualElement BuildSegment()
        {
            var segment = new VisualElement();
            segment.AddToClassList("segment");

            _segmentMarket.text = _text.Or(UiTextKeys.MarketSegmentMarket);
            _segmentMarket.AddToClassList("segment__button");
            _segmentMarket.clicked += () => ShowSquad(false);
            segment.Add(_segmentMarket);

            _segmentSquad.text = _text.Or(UiTextKeys.MarketSegmentSquad);
            _segmentSquad.AddToClassList("segment__button");
            _segmentSquad.clicked += () => ShowSquad(true);
            segment.Add(_segmentSquad);

            return segment;
        }

        private void ShowSquad(bool squad)
        {
            _showingSquad = squad;
            _segmentMarket.EnableInClassList("segment__button--active", !squad);
            _segmentSquad.EnableInClassList("segment__button--active", squad);

            // "Within budget" is a question about buying; on the club's own men it means nothing, so it is
            // hidden rather than left to filter a list it does not apply to.
            _affordableChip.style.display = squad ? DisplayStyle.None : DisplayStyle.Flex;
            Reload();
        }

        // ----- Which players --------------------------------------------------------------------------------

        // The filters sit at the top right of the list, where FM keeps them (UI_REFERENCES §6). Position by
        // LINE rather than by role: four chips fit a phone, twelve do not, and "a defender" is how a manager
        // shops before he narrows to a full-back.
        private VisualElement BuildFilters()
        {
            var chips = new VisualElement();
            chips.AddToClassList("chips");

            chips.Add(LineChip(UiTextKeys.MarketFilterAll, null));
            chips.Add(LineChip(PlayerRoles.GetShortLabelKey(PlayerRole.Goalkeeper), Position.Goalkeeper));
            chips.Add(LineChip(UiTextKeys.SquadDefence, Position.Defender));
            chips.Add(LineChip(UiTextKeys.SquadMidfield, Position.Midfielder));
            chips.Add(LineChip(UiTextKeys.SquadAttack, Position.Forward));

            _affordableChip.text = _text.Or(UiTextKeys.MarketFilterAffordable);
            _affordableChip.AddToClassList("chip");
            _affordableChip.AddToClassList("chip--toggle");
            _affordableChip.clicked += () =>
            {
                _affordableOnly = !_affordableOnly;
                _affordableChip.EnableInClassList("chip--active", _affordableOnly);
                Refilter();
            };
            chips.Add(_affordableChip);

            return chips;
        }

        private Button LineChip(string key, Position? line)
        {
            var chip = new Button(() => ChooseLine(line)) { text = _text.Or(key) };
            chip.AddToClassList("chip");
            chip.userData = line;
            _lineChips.Add(chip);
            return chip;
        }

        private void ChooseLine(Position? line)
        {
            _line = line;
            for (int i = 0; i < _lineChips.Count; i++)
            {
                var chipLine = (Position?)_lineChips[i].userData;
                _lineChips[i].EnableInClassList("chip--active", chipLine == line);
            }

            Refilter();
        }

        // ----- The list -------------------------------------------------------------------------------------

        private VisualElement BuildList()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            card.AddToClassList("card--grow");
            card.AddToClassList("market-list");

            _list.AddToClassList("market-list__view");

            // The ListView's own scroller, reached by query: it is built with the view and is the thing a
            // finger actually moves. Hidden bar, clamped ends, drag-to-scroll — the same three settings as
            // every list on the phone, for the same reasons the squad's list mode records.
            var scroller = _list.Q<ScrollView>();
            if (scroller != null)
            {
                scroller.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
                scroller.verticalScrollerVisibility = ScrollerVisibility.Hidden;
                new DragToScroll(scroller);
            }

            card.Add(_list);

            _empty.text = _text.Or(UiTextKeys.MarketEmpty);
            _empty.AddToClassList("body");
            _empty.AddToClassList("market-list__empty");
            card.Add(_empty);

            return card;
        }

        private void Reload()
        {
            _source.Clear();
            IReadOnlyList<Player> players = _showingSquad ? _session.Squad.Players : _session.GetMarket();
            for (int i = 0; i < players.Count; i++)
            {
                _source.Add(players[i]);
            }

            MarketList.SortByRating(_source);
            Refilter();
        }

        private void Refilter()
        {
            MarketList.Select(_source, _line, _affordableOnly && !_showingSquad ? IsAffordable : null, _shown);
            _list.RefreshItems();

            // Said rather than left blank: an empty list under live filters looks like a list that failed
            // to load, and the fix — loosen a chip — is only obvious if the screen owns the emptiness.
            _empty.style.display = _shown.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private bool IsAffordable(Player player)
        {
            return Affordability.For(_session.FeeOf(player), _session.WeeklyWageOf(player), _session.Finances).Affordable;
        }

        // A two-line tile: the name over the facts, the rating on the right. The row is the target and its
        // labels are not — see SquadScreen.MakePlayerRow for the tap that taught that.
        private VisualElement MakeRow()
        {
            var row = new VisualElement();
            row.AddToClassList("row");
            row.AddToClassList("market-row");
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.currentTarget is VisualElement tapped && tapped.userData is int index)
                {
                    OpenCard(index);
                }
            });

            var text = new VisualElement();
            text.AddToClassList("market-row__text");
            text.pickingMode = PickingMode.Ignore;

            var name = new Label();
            name.AddToClassList("row__name");
            name.pickingMode = PickingMode.Ignore;
            text.Add(name);

            var sub = new Label();
            sub.AddToClassList("market-row__sub");
            sub.pickingMode = PickingMode.Ignore;
            text.Add(sub);
            row.Add(text);

            var rating = new Label();
            rating.AddToClassList("row__rating");
            rating.pickingMode = PickingMode.Ignore;
            row.Add(rating);

            return row;
        }

        private void BindRow(VisualElement element, int index)
        {
            if (index < 0 || index >= _shown.Count)
            {
                return;
            }

            Player player = _shown[index];
            element.userData = index;

            var text = element[0];
            var name = (Label)text[0];
            var sub = (Label)text[1];
            var rating = (Label)element[1];

            name.text = player.Name;
            sub.text = _text.Or(PlayerRoles.GetShortLabelKey(player.Role))
                + "  ·  " + player.Age
                + "  ·  " + _text.Money(_session.FeeOf(player))
                + "  ·  " + _text.Money(_session.WeeklyWageOf(player)) + _text.Or(UiTextKeys.MarketPerWeek);

            double value = PlayerRatings.ForRole(player);
            rating.text = ((int)System.Math.Round(value)).ToString();
            foreach (AbilityBand band in (AbilityBand[])System.Enum.GetValues(typeof(AbilityBand)))
            {
                rating.RemoveFromClassList(AbilityBands.ClassOf(band));
            }

            rating.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of(value)));

            // Dimmed BEFORE the tap. A man the club cannot pay for is still worth reading about — the card
            // says by how much — but the list must not let him look like a choice.
            element.EnableInClassList("row--dim", !_showingSquad && !IsAffordable(player));
        }

        private void OpenCard(int index)
        {
            if (index < 0 || index >= _shown.Count)
            {
                return;
            }

            _host.ShowSheet(new PlayerCard(_session, _text, _host).Build(_shown[index], forSale: _showingSquad));
        }

        // ----- Parts ----------------------------------------------------------------------------------------

        private VisualElement Kv(string labelKey, Label value)
        {
            var kv = new VisualElement();
            kv.AddToClassList("kv");

            var label = new Label(_text.Or(labelKey));
            label.AddToClassList("kv__label");
            kv.Add(label);

            value.AddToClassList("kv__value");
            kv.Add(value);
            return kv;
        }

        private static TextArguments Count(int value)
        {
            return new TextArguments(string.Empty, string.Empty, string.Empty, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            return card;
        }
    }
}
