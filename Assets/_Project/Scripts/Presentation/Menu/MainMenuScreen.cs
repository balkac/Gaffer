using System;
using Gaffer.Application.Run;
using Gaffer.Common.Localization;
using Gaffer.Presentation.Shell;
using UnityEngine.UIElements;

namespace Gaffer.Presentation.Menu
{
    /// <summary>
    /// The first screen: the name of the game, the run you left, and a way to start another. The
    /// owner's call (2026-09-16): "new run" lives here, at the door, and not on a page of the shell where
    /// a destructive tap would sit beside the one the screen exists for.
    ///
    /// <para><b>Continue names what it continues.</b> Club, season and week under the button, so the
    /// manager knows which run he is walking back into before he taps — the line is the same one the
    /// settings sheet writes when it saves.</para>
    ///
    /// <para><b>New run takes two taps when there is something to lose.</b> The first arms the button and
    /// says so; the second starts over. With no run on the door there is nothing to protect and one tap
    /// is right.</para>
    /// </summary>
    public sealed class MainMenuScreen
    {
        private readonly LocalizedStrings _text;
        private readonly RunSession _current;
        private readonly Action _onContinue;
        private readonly Action _onNewRun;

        private bool _newRunArmed;

        /// <param name="current">The run that can be continued — resumed from the save at boot, or the live
        /// one the manager just left — or null when there is none.</param>
        public MainMenuScreen(LocalizedStrings text, RunSession current, Action onContinue, Action onNewRun)
        {
            _text = text;
            _current = current;
            _onContinue = onContinue;
            _onNewRun = onNewRun;
        }

        /// <summary>Takes the whole root: the menu is a screen of its own, not a page of the shell.</summary>
        public void Build(VisualElement root)
        {
            root.Clear();
            root.AddToClassList("theme");
            root.RemoveFromClassList("shell");
            root.AddToClassList("menu");

            var title = new Label(_text.Or(UiTextKeys.MenuTitle));
            title.AddToClassList("menu__title");
            root.Add(title);

            if (_current != null)
            {
                var card = new VisualElement();
                card.AddToClassList("card");

                var line = new Label(RunLines.Describe(_current, _text));
                line.AddToClassList("body");
                line.AddToClassList("menu__run");
                card.Add(line);

                var resume = new Button(_onContinue) { text = _text.Or(UiTextKeys.MenuContinue) };
                resume.AddToClassList("button");
                resume.AddToClassList("button--primary");
                resume.AddToClassList("menu__button");
                card.Add(resume);
                root.Add(card);
            }

            var fresh = new Button() { text = _text.Or(UiTextKeys.MenuNewRun) };
            fresh.AddToClassList("button");
            fresh.AddToClassList("menu__button");
            if (_current == null)
            {
                fresh.AddToClassList("button--primary");
            }

            fresh.clicked += () => NewRun(fresh);
            root.Add(fresh);
        }

        private void NewRun(Button button)
        {
            if (_current != null && !_newRunArmed)
            {
                _newRunArmed = true;
                button.text = _text.Or(UiTextKeys.MenuConfirmNew);
                return;
            }

            _onNewRun();
        }
    }
}
