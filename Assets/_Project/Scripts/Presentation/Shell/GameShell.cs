using System;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Common.Localization;
using Gaffer.Presentation.Market;
using Gaffer.Presentation.Season;
using Gaffer.Presentation.Squad;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Shell
{
    /// <summary>
    /// The navigation shell: three pages, a tab bar under the thumb, and one sheet that rises over all of
    /// it (UI_REFERENCES §6: categories on one hand, depth inside each).
    ///
    /// <para><b>A page is refreshed when it is shown, not when something changes.</b> The screens are
    /// views over render-time queries; the shell has no idea what a signing changed and does not need one
    /// (NON-NEGOTIABLE #4). Showing a tab redraws it from the session, and closing a sheet redraws the page
    /// beneath — so the squad is current after a signing on the market, and the market after a sale.</para>
    ///
    /// <para><b>One sheet for every screen.</b> The report, a player's card, the picker — each is the same
    /// shape: a scrim, a panel, one way out. Held here so a sheet always covers the tab bar; a sheet inside
    /// a page would leave the tabs live beneath it, which is a modal with a side door.</para>
    ///
    /// <para><b>The shell knows whether the run is saved; nobody else does.</b> Screens report a change
    /// through <see cref="RunChanged"/>, the settings sheet asks for a save through the shell, and the
    /// composition root asks <see cref="IsDirty"/> before it autosaves on the way to the background. The
    /// file itself is behind <see cref="IRunPersistence"/>: Presentation never sees a path.</para>
    /// </summary>
    public sealed class GameShell : IScreenHost
    {
        private readonly RunSession _session;
        private readonly VisualElement _root;
        private readonly LocalizedStrings _text;
        private readonly IRunPersistence _persistence;
        private readonly Action _onMenu;
        private bool _dirty;

        private readonly VisualElement _squadPage = new VisualElement();
        private readonly VisualElement _seasonPage = new VisualElement();
        private readonly VisualElement _marketPage = new VisualElement();
        private readonly VisualElement _menuPage = new VisualElement();
        private readonly Button[] _tabs = new Button[4];

        private readonly VisualElement _overlay = new VisualElement();
        private readonly VisualElement _sheet = new VisualElement();
        private readonly VisualElement _sheetPanel = new VisualElement();

        private SquadScreen _squad;
        private SeasonScreen _season;
        private MarketScreen _market;
        private ShellTab _active = ShellTab.Squad;

        /// <param name="persistence">Where Save writes the run.</param>
        /// <param name="onMenu">What leaving does: the composition root takes the root back and shows the
        /// door. The shell does not know what a main menu is.</param>
        public GameShell(RunSession session, VisualElement root, LocalizedStrings text, IRunPersistence persistence, Action onMenu)
        {
            _session = session;
            _root = root;
            _text = text;
            _persistence = persistence;
            _onMenu = onMenu;
        }

        public VisualElement Overlay => _overlay;

        /// <summary>Whether the run has moved since it was last saved (or started).</summary>
        public bool IsDirty => _dirty;

        /// <summary>Writes the run and, if that worked, marks it clean. The one path a save takes, whether
        /// the manager pressed the button or the app went to the background.</summary>
        public Result SaveRun()
        {
            Result saved = _persistence.Save(_session);
            if (saved.IsSuccess)
            {
                _dirty = false;
            }

            return saved;
        }

        public void Build()
        {
            _root.Clear();

            // .theme carries the palette and lives on the shell root, so every page, the bar and the
            // sheet resolve the same tokens; a page that wore it alone would leave the bar colourless.
            _root.AddToClassList("theme");
            _root.RemoveFromClassList("menu");
            _root.AddToClassList("shell");

            var pages = new VisualElement();
            pages.AddToClassList("shell__pages");
            pages.Add(Page(_squadPage));
            pages.Add(Page(_seasonPage));
            pages.Add(Page(_marketPage));
            pages.Add(Page(_menuPage));
            _root.Add(pages);

            _root.Add(BuildTabBar());

            _overlay.AddToClassList("overlay");
            _overlay.pickingMode = PickingMode.Ignore;
            _root.Add(_overlay);

            _root.Add(BuildSheet());

            _squad = new SquadScreen(_session, _squadPage, this, _text);
            _squad.Build();
            _season = new SeasonScreen(_session, _text, onClose: null);
            _market = new MarketScreen(_session, _marketPage, this, _text);
            _market.Build();

            Show(ShellTab.Squad);

            // A run resumed with a decision open opens ON the decision. The Play button would refuse
            // until it is answered anyway; raising the card first says why before anything is refused.
            _squad.ShowDramaIfPending();
        }

        // ----- IScreenHost --------------------------------------------------------------------------------

        public void RunChanged()
        {
            _dirty = true;
        }

        // Straight out: the main menu takes the root over, so there is nothing beneath to refresh.
        private void LeaveToMenu()
        {
            _sheet.style.display = DisplayStyle.None;
            _onMenu?.Invoke();
        }

        public void Show(ShellTab tab)
        {
            _active = tab;
            _squadPage.style.display = tab == ShellTab.Squad ? DisplayStyle.Flex : DisplayStyle.None;
            _seasonPage.style.display = tab == ShellTab.Season ? DisplayStyle.Flex : DisplayStyle.None;
            _marketPage.style.display = tab == ShellTab.Market ? DisplayStyle.Flex : DisplayStyle.None;
            _menuPage.style.display = tab == ShellTab.Menu ? DisplayStyle.Flex : DisplayStyle.None;

            for (int i = 0; i < _tabs.Length; i++)
            {
                _tabs[i].EnableInClassList("tab--active", i == (int)tab);
            }

            Refresh(tab);
        }

        public void ShowSheet(VisualElement page)
        {
            _sheetPanel.Clear();
            _sheetPanel.Add(page);
            _sheet.style.display = DisplayStyle.Flex;
            _sheet.BringToFront();
        }

        public void CloseSheet()
        {
            _sheet.style.display = DisplayStyle.None;
            Refresh(_active);
        }

        // ----- Parts --------------------------------------------------------------------------------------

        private void Refresh(ShellTab tab)
        {
            switch (tab)
            {
                case ShellTab.Squad:
                    _squad.Refresh();
                    break;
                case ShellTab.Season:
                    // Rebuilt whole: the table is a small render-time query and a diff would be a second
                    // model of the league.
                    _seasonPage.Clear();
                    _seasonPage.Add(_season.Build());
                    break;
                case ShellTab.Market:
                    _market.Refresh();
                    break;
                case ShellTab.Menu:
                    // Rebuilt whole, like the season page: the run line changes every week and the
                    // two-tap arming must not survive a visit.
                    _menuPage.Clear();
                    _menuPage.Add(new MenuPage(_session, _text, SaveRun, () => _dirty, LeaveToMenu).Build());
                    break;
            }
        }

        private static VisualElement Page(VisualElement page)
        {
            page.AddToClassList("screen");
            return page;
        }

        private VisualElement BuildTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("tabbar");
            bar.Add(_tabs[(int)ShellTab.Squad] = TabButton(UiTextKeys.NavSquad, ShellTab.Squad));
            bar.Add(_tabs[(int)ShellTab.Season] = TabButton(UiTextKeys.NavSeason, ShellTab.Season));
            bar.Add(_tabs[(int)ShellTab.Market] = TabButton(UiTextKeys.NavMarket, ShellTab.Market));
            bar.Add(_tabs[(int)ShellTab.Menu] = TabButton(UiTextKeys.NavMenu, ShellTab.Menu));
            return bar;
        }

        private Button TabButton(string key, ShellTab tab)
        {
            var button = new Button(() => Show(tab)) { text = _text.Or(key) };
            button.AddToClassList("tab");
            return button;
        }

        private VisualElement BuildSheet()
        {
            _sheet.AddToClassList("sheet");
            _sheet.style.display = DisplayStyle.None;

            // Tapping the dimmed ground dismisses the sheet — the way out has to be as obvious as the way
            // in, and it has to work even when the page inside forgot to offer one.
            var scrim = new VisualElement();
            scrim.AddToClassList("sheet__scrim");
            scrim.RegisterCallback<ClickEvent>(_ => CloseSheet());
            _sheet.Add(scrim);

            _sheetPanel.AddToClassList("sheet__panel");
            _sheetPanel.AddToClassList("sheet__panel--tall");
            _sheet.Add(_sheetPanel);
            return _sheet;
        }
    }
}
