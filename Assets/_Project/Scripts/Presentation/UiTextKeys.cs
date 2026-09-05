using System;
using System.Collections.Generic;
using Gaffer.Application.Simulation;
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

        // ----- match report -------------------------------------------------------------------------------

        public const string MatchGoal = "ui.match.goal";
        public const string MatchShots = "ui.match.shots";
        public const string MatchElsewhere = "ui.match.elsewhere";
        public const string MatchSetup = "ui.match.setup";
        public const string MatchMentality = "ui.match.mentality";
        public const string MatchTempo = "ui.match.tempo";
        public const string MatchPressing = "ui.match.pressing";
        public const string MatchApproach = "ui.match.approach";
        public const string MatchJournal = "ui.match.journal";
        public const string MatchQuiet = "ui.match.quiet";

        // ----- season -------------------------------------------------------------------------------------

        public const string SeasonNext = "ui.season.next";
        public const string SeasonHome = "ui.season.home";
        public const string SeasonAway = "ui.season.away";
        public const string SeasonOver = "ui.season.over";
        public const string SeasonTable = "ui.season.table";
        public const string SeasonPlayed = "ui.season.played";
        public const string SeasonWon = "ui.season.won";
        public const string SeasonDrawn = "ui.season.drawn";
        public const string SeasonLost = "ui.season.lost";
        public const string SeasonGoalDifference = "ui.season.goal_difference";
        public const string SeasonPoints = "ui.season.points";
        public const string SeasonPromotion = "ui.season.promotion";
        public const string SeasonRelegation = "ui.season.relegation";
        public const string SeasonTarget = "ui.season.target";
        public const string SeasonJob = "ui.season.job";
        public const string SeasonTargetUp = "ui.season.target_up";
        public const string SeasonTargetStay = "ui.season.target_stay";

        // ----- actions ------------------------------------------------------------------------------------

        public const string ActionAutoPick = "ui.action.auto_pick";
        public const string ActionPlayWeek = "ui.action.play_week";
        public const string ActionContinue = "ui.action.continue";
        public const string ActionClose = "ui.action.close";

        // ----- messages -----------------------------------------------------------------------------------

        public const string MessageNoFixture = "ui.message.no_fixture";

        private static readonly string[] ScreenKeys =
        {
            SquadEleven, SquadBench, SquadPosition, SquadWeek,
            SquadAttack, SquadMidfield, SquadDefence, ViewPitch, ViewList, PickerWho, PickerWhere,
            MatchGoal, MatchShots, MatchElsewhere, MatchSetup,
            MatchMentality, MatchTempo, MatchPressing, MatchApproach, MatchJournal, MatchQuiet,
            SeasonNext, SeasonHome, SeasonAway, SeasonOver, SeasonTable,
            SeasonPlayed, SeasonWon, SeasonDrawn, SeasonLost, SeasonGoalDifference, SeasonPoints,
            SeasonPromotion, SeasonRelegation, SeasonTarget, SeasonJob, SeasonTargetUp, SeasonTargetStay,
            ActionAutoPick, ActionPlayWeek, ActionContinue, ActionClose,
            MessageNoFixture,
        };

        private static readonly string[] AllKeys = Build();

        /// <summary>Every key the screens ask for: the constants above, plus one abbreviation per
        /// <see cref="PlayerRole"/> and one name per setting on the four tactical axes. A key added
        /// without reaching this list is a key nothing guards.</summary>
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
            var keys = new List<string>(ScreenKeys.Length + 32);
            keys.AddRange(ScreenKeys);

            foreach (PlayerRole role in (PlayerRole[])Enum.GetValues(typeof(PlayerRole)))
            {
                keys.Add(PlayerRoles.GetShortLabelKey(role));
            }

            foreach (Mentality value in (Mentality[])Enum.GetValues(typeof(Mentality)))
            {
                keys.Add(TacticsTextKeys.For(value));
            }

            foreach (Tempo value in (Tempo[])Enum.GetValues(typeof(Tempo)))
            {
                keys.Add(TacticsTextKeys.For(value));
            }

            foreach (Pressing value in (Pressing[])Enum.GetValues(typeof(Pressing)))
            {
                keys.Add(TacticsTextKeys.For(value));
            }

            foreach (Approach value in (Approach[])Enum.GetValues(typeof(Approach)))
            {
                keys.Add(TacticsTextKeys.For(value));
            }

            return keys.ToArray();
        }
    }
}
