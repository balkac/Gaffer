using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Players;
using Gaffer.Presentation.Shell;
using Gaffer.Presentation.Squad;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Market
{
    /// <summary>
    /// One player, and the one thing you can do about him. The card behind a market tile (UI_REFERENCES §2):
    /// who he is, what the scouts can see, what he costs, whether that works — and a single button.
    ///
    /// <para><b>The answer comes before the button.</b> The money card says in the manager's units what
    /// the signing leaves or by how much it misses (<see cref="Affordability"/>), and a sale says whether
    /// he starts and whether the squad can spare him. Nothing on this card is learned by pressing.</para>
    ///
    /// <para><b>The result is shown where the command was given</b> (UI_REFERENCES §4.4): a signing turns
    /// this card into its own receipt — signed, for this fee, leaving this much — with Done the only way out.
    /// The manager never goes looking for what happened.</para>
    ///
    /// <para><b>Scout uncertainty is drawn as a band</b> (ART_STYLE §4.1): a range on a track, never a
    /// number pretending to be known. The width IS the information.</para>
    /// </summary>
    public sealed class PlayerCard
    {
        private readonly RunSession _session;
        private readonly LocalizedStrings _text;
        private readonly IScreenHost _host;

        private readonly VisualElement _body = new VisualElement();
        private readonly VisualElement _action = new VisualElement();
        private readonly Label _refusal = new Label();

        public PlayerCard(RunSession session, LocalizedStrings text, IScreenHost host)
        {
            _session = session;
            _text = text;
            _host = host;
        }

        /// <summary>The card for one man: to buy him off the market, or to sell him out of the squad.</summary>
        public VisualElement Build(Player player, bool forSale)
        {
            var root = new VisualElement();
            root.AddToClassList("page");

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("page__scroll");
            scroll.touchScrollBehavior = ScrollView.TouchScrollBehavior.Clamped;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            new DragToScroll(scroll);

            _body.AddToClassList("page__body");
            _body.Add(BuildWho(player));
            _body.Add(BuildReport(player, known: forSale));
            _body.Add(forSale ? BuildSaleTerms(player) : BuildSigningTerms(player));
            scroll.Add(_body);
            root.Add(scroll);

            // The one action, pinned beneath the scroller like every page's way out. Built into a slot so
            // the receipt can replace it with Done.
            _action.AddToClassList("page__action-slot");
            FillAction(player, forSale);
            root.Add(_action);

            return root;
        }

        // ----- Who ------------------------------------------------------------------------------------------

        private VisualElement BuildWho(Player player)
        {
            VisualElement card = Card();

            var name = new Label(player.Name);
            name.AddToClassList("headline");
            card.Add(name);

            var line = new Label(
                _text.Or(PlayerRoles.GetShortLabelKey(player.Role))
                + "  ·  " + _text.Or(UiTextKeys.MarketAge) + " " + player.Age);
            line.AddToClassList("label");
            card.Add(line);

            return card;
        }

        // ----- What the scouts see --------------------------------------------------------------------------

        /// <param name="known">Whether this is one of the club's own men. The scouts' mask is for prospects;
        /// a player the manager trains every day is read at full accuracy, so his bands collapse to the
        /// numbers the squad screen already shows him by — a card that blurred him would contradict the
        /// board a tap away.</param>
        private VisualElement BuildReport(Player player, bool known)
        {
            VisualElement card = Card();
            card.Add(Eyebrow(_text.Or(UiTextKeys.MarketReport)));

            ScoutReport report = known ? _session.Observe(player, 1.0) : _session.Observe(player);

            // Potential first and on its own line: it is the one number the whole screen is about — what
            // the rivals cannot see and the manager is betting on.
            card.Add(Range(_text.Or(UiTextKeys.MarketPotential), report.PotentialLow, report.PotentialHigh, wide: true));

            IReadOnlyList<AttributeEstimate> estimates = report.KeyAttributes;
            for (int i = 0; i < estimates.Count; i++)
            {
                card.Add(Range(_text.Or(estimates[i].LabelKey), estimates[i].Low, estimates[i].High, wide: false));
            }

            return card;
        }

        /// <summary>A range on a 1–99 track. The band sits where the truth might be; the numbers beside it say
        /// the same thing for anyone who reads numbers first. Brightness follows the midpoint, so a good
        /// unknown still reads as good.</summary>
        private static VisualElement Range(string label, int low, int high, bool wide)
        {
            var row = new VisualElement();
            row.AddToClassList("range");

            var name = new Label(label);
            name.AddToClassList("range__label");
            if (wide)
            {
                name.AddToClassList("range__label--wide");
            }

            row.Add(name);

            var track = new VisualElement();
            track.AddToClassList("range__track");

            var band = new VisualElement();
            band.AddToClassList("range__band");
            band.style.left = Length.Percent(low);
            band.style.width = Length.Percent(System.Math.Max(high - low, 1));
            track.Add(band);
            row.Add(track);

            // One number when the band has no width: "72–72" is a range that has stopped being one.
            var value = new Label(low == high ? low.ToString() : low + "–" + high);
            value.AddToClassList("range__value");
            value.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of((low + high) / 2.0)));
            row.Add(value);

            return row;
        }

        // ----- What it costs, and whether that works --------------------------------------------------------

        private VisualElement BuildSigningTerms(Player player)
        {
            VisualElement card = Card();
            long fee = _session.FeeOf(player);
            long wage = _session.WeeklyWageOf(player);

            var money = new VisualElement();
            money.AddToClassList("kv-row");
            money.Add(Kv(UiTextKeys.MarketFee, _text.Money(fee)));
            money.Add(Kv(UiTextKeys.MarketWage, _text.Money(wage) + _text.Or(UiTextKeys.MarketPerWeek)));
            card.Add(money);

            Affordability verdict = Affordability.For(fee, wage, _session.Finances);
            if (verdict.CashShort && verdict.WageShort)
            {
                card.Add(Note(_text.Or(UiTextKeys.MarketShortBoth)));
            }
            else if (verdict.CashShort)
            {
                card.Add(Note(_text.Or(UiTextKeys.MarketShortCash, Money(-verdict.CashLeft))));
            }
            else if (verdict.WageShort)
            {
                card.Add(Note(_text.Or(UiTextKeys.MarketShortWage, Money(-verdict.WageRoomLeft))));
            }
            else
            {
                card.Add(Note(_text.Or(UiTextKeys.MarketLeavesCash, Money(verdict.CashLeft))));
                card.Add(Note(_text.Or(UiTextKeys.MarketLeavesWage, Money(verdict.WageRoomLeft))));
            }

            AddWindowNote(card);
            card.Add(Refusal());
            return card;
        }

        private VisualElement BuildSaleTerms(Player player)
        {
            VisualElement card = Card();

            var money = new VisualElement();
            money.AddToClassList("kv-row");
            money.Add(Kv(UiTextKeys.MarketFee, _text.Money(_session.FeeOf(player))));
            money.Add(Kv(UiTextKeys.MarketWage, _text.Money(_session.WeeklyWageOf(player)) + _text.Or(UiTextKeys.MarketPerWeek)));
            card.Add(money);

            // What he is to the side today, said before the fee tempts anyone. A starter sold is a hole in
            // the eleven on Saturday, and the card is where that is learned rather than the board.
            if (IsStarter(player))
            {
                card.Add(Note(_text.Or(UiTextKeys.MarketStarter)));
            }

            if (!CanSpareAnyone())
            {
                card.Add(Note(_text.Or(UiTextKeys.MarketSquadFloor, Count(_session.Squad.Count))));
            }

            AddWindowNote(card);
            card.Add(Refusal());
            return card;
        }

        // Said only when the window is shut, and then it is the reason there is no button.
        private void AddWindowNote(VisualElement card)
        {
            if (_session.IsWindowOpen)
            {
                return;
            }

            int? reopens = TransferWindow.NextOpening(_session.PlayedRounds, _session.RoundCount);
            card.Add(Note(reopens.HasValue
                ? _text.Or(UiTextKeys.MarketOpensAfter, Count(reopens.Value))
                : _text.Or(UiTextKeys.MarketOpensSummer)));
        }

        // ----- The one action -------------------------------------------------------------------------------

        private void FillAction(Player player, bool forSale)
        {
            _action.Clear();

            // No button when the window is shut: a button that only ever refuses teaches the manager to
            // stop reading buttons. The note above says when to come back.
            if (!_session.IsWindowOpen)
            {
                return;
            }

            if (forSale)
            {
                var sell = Primary(_text.Or(UiTextKeys.ActionSell) + "  ·  " + _text.Money(_session.FeeOf(player)), () => Sell(player));
                sell.SetEnabled(CanSpareAnyone());
                _action.Add(sell);
                return;
            }

            long fee = _session.FeeOf(player);
            long wage = _session.WeeklyWageOf(player);
            var sign = Primary(
                _text.Or(UiTextKeys.ActionSign) + "  ·  " + _text.Money(fee) + "  ·  " + _text.Money(wage) + _text.Or(UiTextKeys.MarketPerWeek),
                () => Sign(player));
            sign.SetEnabled(Affordability.For(fee, wage, _session.Finances).Affordable);
            _action.Add(sign);
        }

        private void Sign(Player player)
        {
            Receive(_session.SignPlayer(player), forSale: false);
        }

        private void Sell(Player player)
        {
            Receive(_session.SellPlayer(player), forSale: true);
        }

        // The outcome becomes the card. On a refusal the core's sentence is shown in place, exactly as the
        // squad screen shows its refusals — the rule was pre-checked above, so this is the rare path.
        private void Receive(Result<TransferOutcome> result, bool forSale)
        {
            if (result.IsFailure)
            {
                _refusal.text = result.Error;
                _refusal.style.display = DisplayStyle.Flex;
                return;
            }

            TransferOutcome outcome = result.Value;
            _body.Clear();
            _body.Add(BuildReceipt(outcome, forSale));

            _action.Clear();
            _action.Add(Primary(_text.Or(UiTextKeys.ActionDone), _host.CloseSheet));
        }

        private VisualElement BuildReceipt(TransferOutcome outcome, bool forSale)
        {
            VisualElement card = Card();

            var name = new Label(outcome.Player.Name);
            name.AddToClassList("headline");
            card.Add(name);

            card.Add(Eyebrow(forSale
                ? _text.Or(UiTextKeys.MarketSold, Money(outcome.Fee))
                : _text.Or(UiTextKeys.MarketSigned)));

            var money = new VisualElement();
            money.AddToClassList("kv-row");
            money.Add(Kv(UiTextKeys.MarketFee, _text.Money(outcome.Fee)));
            money.Add(Kv(UiTextKeys.MarketWage, _text.Money(outcome.WeeklyWage) + _text.Or(UiTextKeys.MarketPerWeek)));
            card.Add(money);

            card.Add(Note(_text.Or(UiTextKeys.MarketLeavesCash, Money(outcome.Finances.Cash))));
            card.Add(Note(_text.Or(UiTextKeys.MarketLeavesWage, Money(outcome.Finances.WageHeadroom))));
            return card;
        }

        // ----- Questions of the run -------------------------------------------------------------------------

        private bool IsStarter(Player player)
        {
            IReadOnlyList<Player> slots = _session.Lineup().Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Id == player.Id)
                {
                    return true;
                }
            }

            return false;
        }

        // The core's floor, read from the core: a sale that would leave fewer than an eleven is refused
        // there, and the card says so first rather than mirroring the number.
        private bool CanSpareAnyone()
        {
            return _session.Squad.Count > TransferService.MinSquadSize;
        }

        // ----- Parts ----------------------------------------------------------------------------------------

        private Label Refusal()
        {
            _refusal.AddToClassList("body");
            _refusal.AddToClassList("card__refusal");
            _refusal.style.display = DisplayStyle.None;
            return _refusal;
        }

        private static Button Primary(string words, System.Action onTapped)
        {
            var button = new Button(onTapped) { text = words };
            button.AddToClassList("button");
            button.AddToClassList("button--primary");
            button.AddToClassList("page__action");
            return button;
        }

        private VisualElement Kv(string labelKey, string words)
        {
            var kv = new VisualElement();
            kv.AddToClassList("kv");

            var label = new Label(_text.Or(labelKey));
            label.AddToClassList("kv__label");
            kv.Add(label);

            var value = new Label(words);
            value.AddToClassList("kv__value");
            kv.Add(value);
            return kv;
        }

        private static Label Note(string words)
        {
            var label = new Label(words);
            label.AddToClassList("body");
            label.AddToClassList("card__note");
            return label;
        }

        private static Label Eyebrow(string words)
        {
            var label = new Label(words);
            label.AddToClassList("label");
            return label;
        }

        private static VisualElement Card()
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            return card;
        }

        private TextArguments Money(long value)
        {
            return new TextArguments(string.Empty, string.Empty, string.Empty, _text.Money(value));
        }

        private static TextArguments Count(int value)
        {
            return new TextArguments(string.Empty, string.Empty, string.Empty, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }
}
