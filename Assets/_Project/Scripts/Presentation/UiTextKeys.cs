using System.Collections.Generic;

namespace Gaffer.Presentation
{
    /// <summary>
    /// Every localization key the screens ask for, in one place.
    ///
    /// <para><b>Why a list and not just constants scattered where they are used.</b> The guard that
    /// matters is "every key a screen shows has words in every shipped locale", and that guard can only
    /// run if the keys can be ENUMERATED. Scattered literals cannot be; a missing one then shows up as a
    /// blank label on a device, in a language the person who wrote it does not read.</para>
    ///
    /// <para>Framework-free on purpose, so <c>dotnet test</c> can check the coverage without opening
    /// Unity (CLAUDE.md, test bridge).</para>
    /// </summary>
    public static class UiTextKeys
    {
        // ----- squad screen -------------------------------------------------------------------------------

        public const string SquadEleven = "ui.squad.eleven";
        public const string SquadBench = "ui.squad.bench";
        public const string SquadPosition = "ui.squad.position";
        public const string SquadWeek = "ui.squad.week";
        public const string SquadAttack = "ui.squad.attack";
        public const string SquadMidfield = "ui.squad.midfield";
        public const string SquadDefence = "ui.squad.defence";

        // ----- actions ------------------------------------------------------------------------------------

        public const string ActionAutoPick = "ui.action.auto_pick";
        public const string ActionPlayWeek = "ui.action.play_week";

        // ----- messages -----------------------------------------------------------------------------------

        public const string MessageNoFixture = "ui.message.no_fixture";

        private static readonly string[] AllKeys =
        {
            SquadEleven, SquadBench, SquadPosition, SquadWeek,
            SquadAttack, SquadMidfield, SquadDefence,
            ActionAutoPick, ActionPlayWeek,
            MessageNoFixture,
        };

        /// <summary>Every key above. A key added without being listed here is a key nothing guards.</summary>
        public static IReadOnlyList<string> All => AllKeys;
    }
}
