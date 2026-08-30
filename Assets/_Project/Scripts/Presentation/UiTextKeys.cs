using System;
using System.Collections.Generic;
using Gaffer.Domain.Players;

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
        public const string ViewPitch = "ui.squad.view_pitch";
        public const string ViewList = "ui.squad.view_list";
        public const string PickerWho = "ui.squad.picker_who";
        public const string PickerWhere = "ui.squad.picker_where";

        // ----- actions ------------------------------------------------------------------------------------

        public const string ActionAutoPick = "ui.action.auto_pick";
        public const string ActionPlayWeek = "ui.action.play_week";

        // ----- messages -----------------------------------------------------------------------------------

        public const string MessageNoFixture = "ui.message.no_fixture";

        private static readonly string[] ScreenKeys =
        {
            SquadEleven, SquadBench, SquadPosition, SquadWeek,
            SquadAttack, SquadMidfield, SquadDefence, ViewPitch, ViewList, PickerWho, PickerWhere,
            ActionAutoPick, ActionPlayWeek,
            MessageNoFixture,
        };

        private static readonly string[] AllKeys = Build();

        /// <summary>Every key the screens ask for: the constants above, plus one abbreviation per
        /// <see cref="PlayerRole"/>. A key added without reaching this list is a key nothing guards.</summary>
        public static IReadOnlyList<string> All => AllKeys;

        /// <summary>
        /// The role abbreviations are DERIVED from the enum rather than listed by hand, because a list
        /// by hand is a list that goes stale: adding a role to <see cref="PlayerRole"/> would otherwise
        /// leave a key nothing guards and a tile that throws the first time it is drawn. This is also
        /// what makes an authored <c>StringTableSO</c> have to supply them — <c>GameRoot</c> validates a
        /// table against exactly this list.
        /// </summary>
        private static string[] Build()
        {
            var roles = (PlayerRole[])Enum.GetValues(typeof(PlayerRole));
            var keys = new string[ScreenKeys.Length + roles.Length];

            for (int i = 0; i < ScreenKeys.Length; i++)
            {
                keys[i] = ScreenKeys[i];
            }

            for (int i = 0; i < roles.Length; i++)
            {
                keys[ScreenKeys.Length + i] = PlayerRoles.GetShortLabelKey(roles[i]);
            }

            return keys;
        }
    }
}
