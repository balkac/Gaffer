using System;
using Gaffer.Application.Run;
using Gaffer.Common;
using Gaffer.Common.Localization;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Shell
{
    /// <summary>
    /// The menu tab: the run's two manual verbs, Save and Main menu. A page of the shell, one tap from
    /// anywhere — it began life as a sheet behind a button in the squad header, and the owner said what
    /// that was worth: "kesinlikle belli olmuyor" (2026-09-16). A verb the manager has to find is not a
    /// verb he has. From Faz 6 this page is also where the manager himself lives.
    ///
    /// <para><b>Saving is manual by the owner's choice (2026-09-16), like FM's</b>: the manager saves
    /// when he means to, and the shell autosaves silently when the app goes to the background so a
    /// phone that kills it does not take the unsaved weeks with it. The sheet says what it saved (club,
    /// season, week) rather than only that it did, because "Saved." with nothing under it is a promise
    /// the manager cannot check.</para>
    ///
    /// <para><b>Leaving with unsaved progress takes two taps.</b> The first tap says what is at stake and
    /// arms the button; the second leaves. No dialog: a modal on a modal is where phone interfaces go to
    /// die, and the sentence sits right where the thumb already is.</para>
    /// </summary>
    public sealed class MenuPage
    {
        private readonly RunSession _session;
        private readonly LocalizedStrings _text;
        private readonly Func<Result> _save;
        private readonly Func<bool> _isDirty;
        private readonly Action _onMenu;

        private readonly Label _status = new Label();
        private bool _leaveArmed;

        public MenuPage(RunSession session, LocalizedStrings text, Func<Result> save, Func<bool> isDirty, Action onMenu)
        {
            _session = session;
            _text = text;
            _save = save;
            _isDirty = isDirty;
            _onMenu = onMenu;
        }

        public VisualElement Build()
        {
            var root = new VisualElement();
            root.AddToClassList("page");

            var body = new VisualElement();
            body.AddToClassList("page__body");

            var eyebrow = new Label(_text.Or(UiTextKeys.SettingsTitle));
            eyebrow.AddToClassList("label");
            body.Add(eyebrow);

            var card = new VisualElement();
            card.AddToClassList("card");
            card.AddToClassList("settings__card");

            var run = new Label(RunLine());
            run.AddToClassList("headline");
            run.AddToClassList("settings__run");
            card.Add(run);

            var save = new Button(Save) { text = _text.Or(UiTextKeys.ActionSave) };
            save.AddToClassList("button");
            save.AddToClassList("button--primary");
            save.AddToClassList("settings__button");
            card.Add(save);

            var menu = new Button(Leave) { text = _text.Or(UiTextKeys.SettingsMenu) };
            menu.AddToClassList("button");
            menu.AddToClassList("settings__button");
            card.Add(menu);

            _status.AddToClassList("settings__status");
            _status.style.display = DisplayStyle.None;
            card.Add(_status);

            body.Add(card);
            root.Add(body);
            return root;
        }

        private void Save()
        {
            Result saved = _save();
            _leaveArmed = false;
            Say(saved.IsSuccess ? _text.Or(UiTextKeys.SettingsSaved) + "  " + RunLine() : saved.Error);
        }

        private void Leave()
        {
            if (_isDirty() && !_leaveArmed)
            {
                _leaveArmed = true;
                Say(_text.Or(UiTextKeys.SettingsUnsaved));
                return;
            }

            _onMenu();
        }

        private void Say(string words)
        {
            _status.text = words;
            _status.style.display = string.IsNullOrEmpty(words) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        // "Coldwold · season 2  ·  WEEK 14/38" — the same line the main menu writes under Continue, so
        // what was saved and what can be resumed are described in the same words.
        private string RunLine()
        {
            return RunLines.Describe(_session, _text);
        }
    }

    /// <summary>One sentence that names a run: club, season, week. Written once because two screens say it.</summary>
    public static class RunLines
    {
        public static string Describe(RunSession session, LocalizedStrings text)
        {
            string season = session.SeasonNumber.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return text.Or(UiTextKeys.MenuRunLine, new TextArguments(string.Empty, session.ManagedClubName, string.Empty, season))
                + "  ·  " + text.Or(UiTextKeys.SquadWeek) + " " + session.PlayedRounds + "/" + session.RoundCount;
        }
    }
}
