using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using UnityEngine;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Squad
{
    /// <summary>
    /// The squad and tactics screen — the first of the five, and the one the manager touches most.
    ///
    /// <para><b>A view over outcomes, not a second copy of the game.</b> It renders
    /// <see cref="LineupOutcome"/> and sends commands back; it never reaches into the run to diff state
    /// (NON-NEGOTIABLE #4). Every command it issues returns the outcome it should draw next, so the screen
    /// has no idea what changed and does not need one — which is what stops a UI drifting away from the
    /// simulation, the exact failure the two editor windows had before <c>RunSession</c> existed.</para>
    ///
    /// <para><b>Built in C# rather than UXML</b>, like the editor windows: the layout is entirely
    /// data-driven, so a markup file would be a near-empty shell plus a second place to keep names in
    /// step. The look is not here either — every colour and size comes from <c>Gaffer.uss</c>, and the
    /// only styling decision this file makes is which CLASS a thing wears.</para>
    ///
    /// <para><b>Mobile first.</b> The eleven and the bench are <see cref="ListView"/>s, so rows are
    /// recycled rather than built per player — a squad grows without bound (contracts are not written
    /// yet) and a market list will do the same. Every row is a 44px tap target.</para>
    /// </summary>
    public sealed class SquadScreen
    {
        private const int RowHeight = 56;

        private readonly RunSession _session;
        private readonly VisualElement _root;

        // The words. Injected rather than reached for: Presentation may not see the string table's
        // assembly (NON-NEGOTIABLE #8's layer half), and Composition is what binds a locale to a screen —
        // which is also what lets the language change without this class knowing there are languages.
        private readonly LocalizedStrings _text;

        private readonly Label _clubName = new Label();
        private readonly Label _standing = new Label();
        private readonly Label _shape = new Label();
        private readonly ListView _eleven = new ListView();
        private readonly ListView _bench = new ListView();
        private readonly Label _message = new Label();

        // The lists ListView binds against. Held and refilled rather than replaced, so a rebind does not
        // hand the view a different collection every time (PERFORMANCE §8).
        private readonly List<Player> _starters = new List<Player>();
        private readonly List<Player> _benched = new List<Player>();

        public SquadScreen(RunSession session, VisualElement root, LocalizedStrings text)
        {
            _session = session;
            _root = root;
            _text = text;
        }

        /// <summary>Builds the screen once and draws the run's current state into it.</summary>
        public void Build()
        {
            _root.Clear();

            // .theme carries the palette, .screen the ground it paints. Both, because a screen that wore
            // only the second would resolve every var() to nothing and fall back to Unity's default
            // runtime theme — which is exactly what a missing stylesheet looks like on a device.
            _root.AddToClassList("theme");
            _root.AddToClassList("screen");

            _root.Add(BuildHeader());
            _root.Add(BuildList(Say(UiTextKeys.SquadEleven), _eleven, _starters, OnStarterTapped));
            _root.Add(BuildList(Say(UiTextKeys.SquadBench), _bench, _benched, OnBenchTapped));
            _root.Add(BuildActions());

            _message.AddToClassList("body");
            _root.Add(_message);

            Draw(_session.Lineup());
        }

        // ----- Drawing ------------------------------------------------------------------------------------

        // Everything the screen shows, from one outcome. There is no other path in: a command's result
        // comes back through here too, so "what does the screen show" has one answer.
        private void Draw(LineupOutcome lineup)
        {
            _clubName.text = _session.ManagedClubName;
            string week = Say(UiTextKeys.SquadWeek) + " " + _session.PlayedRounds + "/" + _session.RoundCount;
            _standing.text = _session.TablePosition > 0
                ? Say(UiTextKeys.SquadPosition) + " " + _session.TablePosition + "  ·  " + week
                : week;

            TeamStrength strength = lineup.Strength;
            _shape.text = lineup.Formation.Name
                + "   " + Say(UiTextKeys.SquadAttack) + " " + Rounded(strength.Attack)
                + "   " + Say(UiTextKeys.SquadMidfield) + " " + Rounded(strength.Midfield)
                + "   " + Say(UiTextKeys.SquadDefence) + " " + Rounded(strength.Defence);

            Refill(_starters, lineup.Starters);
            Refill(_benched, lineup.Bench);
            _eleven.Rebuild();
            _bench.Rebuild();
        }

        private static void Refill(List<Player> into, IReadOnlyList<Player> from)
        {
            into.Clear();
            for (int i = 0; i < from.Count; i++)
            {
                into.Add(from[i]);
            }
        }

        // ----- Layout -------------------------------------------------------------------------------------

        private VisualElement BuildHeader()
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            _clubName.AddToClassList("headline");
            _standing.AddToClassList("label");
            _shape.AddToClassList("body");

            card.Add(_clubName);
            card.Add(_standing);
            card.Add(_shape);
            return card;
        }

        private VisualElement BuildList(string heading, ListView view, List<Player> source, System.Action<int> onTapped)
        {
            var card = new VisualElement();
            card.AddToClassList("card");

            var title = new Label(heading);
            title.AddToClassList("label");
            card.Add(title);

            view.itemsSource = source;
            view.fixedItemHeight = RowHeight;
            view.selectionType = SelectionType.None;
            view.style.minHeight = RowHeight * 3;
            view.style.flexGrow = 1;
            view.showBorder = false;

            // makeItem/bindItem are the recycling contract: makeItem runs once per VISIBLE row and
            // bindItem every time one is reused, so bindItem must set every property it ever sets —
            // a class added on one row and not removed on the next is the classic ListView bug.
            view.makeItem = () => MakePlayerRow(onTapped);
            view.bindItem = (element, index) => BindPlayerRow(element, source, index);

            card.Add(view);
            return card;
        }

        // Layout and look both come from the stylesheet; this only says what the parts ARE. Inline styles
        // here would be the palette leaking into C# one property at a time.
        private static VisualElement MakePlayerRow(System.Action<int> onTapped)
        {
            var row = new VisualElement();
            row.AddToClassList("row");

            // The click is registered on the ROW, and the row reads its own index.
            //
            // It used to be one handler on the ListView reading evt.target, and that was a real bug: the
            // target is the DEEPEST element under the finger, so tapping a player's NAME delivered the
            // Label — which carries no index — and the tap was swallowed. Only the gaps between the labels
            // worked, which reads as "it misses about half my taps".
            //
            // The children are picking-disabled as well, so the row is one target rather than four. Both
            // halves are needed: without the first the row never hears the tap, without the second the
            // label eats it before the row can.
            row.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.currentTarget is VisualElement tapped && tapped.userData is int index)
                {
                    onTapped(index);
                }
            });

            var name = new Label();
            name.AddToClassList("row__name");
            name.pickingMode = PickingMode.Ignore;
            row.Add(name);

            var role = new Label();
            role.AddToClassList("row__role");
            role.pickingMode = PickingMode.Ignore;
            row.Add(role);

            var rating = new Label();
            rating.AddToClassList("row__rating");
            rating.pickingMode = PickingMode.Ignore;
            row.Add(rating);

            return row;
        }

        private static void BindPlayerRow(VisualElement element, List<Player> source, int index)
        {
            if (index < 0 || index >= source.Count)
            {
                return;
            }

            Player player = source[index];
            element.userData = index;

            var name = (Label)element[0];
            var role = (Label)element[1];
            var rating = (Label)element[2];

            name.text = player.Name;
            role.text = player.Role.ToString().Substring(0, 3).ToUpperInvariant();

            double value = PlayerRatings.ForRole(player);
            rating.text = Rounded(value);

            // EVERY band class is removed before the right one is added. A reused row carries whatever the
            // last player left on it otherwise, and the bug shows up as one row in a scrolled list wearing
            // somebody else's brightness.
            foreach (AbilityBand band in (AbilityBand[])System.Enum.GetValues(typeof(AbilityBand)))
            {
                rating.RemoveFromClassList(AbilityBands.ClassOf(band));
            }

            rating.AddToClassList(AbilityBands.ClassOf(AbilityBands.Of(value)));
        }

        private VisualElement BuildActions()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            var autoPick = new Button(OnAutoPick) { text = Say(UiTextKeys.ActionAutoPick) };
            autoPick.AddToClassList("button");
            autoPick.style.flexGrow = 1;
            autoPick.style.marginRight = 8;

            var advance = new Button(OnAdvanceWeek) { text = Say(UiTextKeys.ActionPlayWeek) };
            advance.AddToClassList("button");
            advance.AddToClassList("button--primary");
            advance.style.flexGrow = 1;

            row.Add(autoPick);
            row.Add(advance);
            return row;
        }

        // ----- Commands -----------------------------------------------------------------------------------

        // Every one of these does the same thing: issue a command, draw what it returns, and say so when
        // it refuses. A refusal is a sentence the core wrote, not a code the screen interprets.

        private void OnStarterTapped(int index)
        {
            if (index >= 0 && index < _starters.Count)
            {
                Apply(_session.ToggleStarter(_starters[index].Id));
            }
        }

        private void OnBenchTapped(int index)
        {
            if (index >= 0 && index < _benched.Count)
            {
                Apply(_session.ToggleStarter(_benched[index].Id));
            }
        }

        private void OnAutoPick()
        {
            Apply(_session.AutoPickLineup());
        }

        private void OnAdvanceWeek()
        {
            Result<WeekOutcome> week = _session.AdvanceWeek();
            if (week.IsFailure)
            {
                ShowMessage(week.Error);
                return;
            }

            ShowMessage(Describe(week.Value));
            Draw(_session.Lineup());
        }

        private void Apply(Result<LineupOutcome> result)
        {
            if (result.IsFailure)
            {
                ShowMessage(result.Error);
                return;
            }

            ShowMessage(string.Empty);
            Draw(result.Value);
        }

        // A key's words, or the key itself when the table has no row for it. Visible-but-wrong beats blank:
        // a missing line shows up as "ui.squad.bench" on screen, which names its own fix, where an empty
        // label just looks like a layout bug.
        private string Say(string key)
        {
            string words = _text.IsBound ? _text.Find(key) : null;
            return string.IsNullOrEmpty(words) ? key : words;
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _message.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private string Describe(WeekOutcome week)
        {
            if (week.ManagedMatch == null)
            {
                return Say(UiTextKeys.MessageNoFixture);
            }

            MatchResult match = week.ManagedMatch.Value;
            return _session.ClubName(match.Home) + " " + match.HomeGoals + "-" + match.AwayGoals + " " + _session.ClubName(match.Away);
        }

        private static string Rounded(double value)
        {
            return Mathf.RoundToInt((float)value).ToString();
        }
    }
}
