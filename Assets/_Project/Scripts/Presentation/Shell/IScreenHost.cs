using UnityEngine.UIElements;

namespace Gaffer.Presentation.Shell
{
    /// <summary>
    /// What a screen may ask of the thing that holds it — and nothing more. A screen never reaches its
    /// neighbours; it asks the host to show a tab, to raise a sheet over everything (the tab bar included,
    /// which is why a screen cannot do that itself), and for the one layer that must sit above the page
    /// and below any sheet: where a drag ghost and a picker draw.
    /// </summary>
    public interface IScreenHost
    {
        /// <summary>The layer above every page and beneath any sheet. Absolutely positioned over the whole
        /// shell, so a picker parented here dims the tab bar too — a sheet that left the tabs live would be
        /// a modal with a side door.</summary>
        VisualElement Overlay { get; }

        /// <summary>Raises a page in the tall sheet, over everything. The page brings its own scroller and
        /// its own way out.</summary>
        void ShowSheet(VisualElement page);

        /// <summary>Lowers the sheet and refreshes the page beneath it, because whatever the sheet did —
        /// a week played, a man signed — the page was drawn before it happened.</summary>
        void CloseSheet();

        void Show(ShellTab tab);

        /// <summary>Tells the host the run moved — a week played, an answer given, a man signed, the eleven
        /// changed. The host is the one thing that knows whether that has been saved yet; a screen never
        /// saves, it only says.</summary>
        void RunChanged();
    }
}
