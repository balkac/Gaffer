using System.Globalization;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Common.Localization;

namespace Gaffer.Editor.Harness
{
    /// <summary>
    /// Renders a recognised <see cref="CareerMoment"/> into the line a player would read — through the
    /// string table, in whichever locale <c>HarnessCopy</c> is set to.
    ///
    /// <para><b>Not the <c>HarnessLabels</c> exemption</b>, for the same reason drama copy is not: these
    /// are player-facing CONTENT, they ship, they are translated. The dev-tool part is only that a
    /// window is the thing asking. Putting them anywhere else would quietly re-open the door
    /// NON-NEGOTIABLE #8 closes.</para>
    ///
    /// <para><b>Why it exists at all before Faz 7.</b> Gate B asks whether these lines read as though
    /// somebody wrote them, and that cannot be judged from a key. Seeing them next to the scoreline that
    /// produced them — in Turkish as easily as English, since the locale is a menu item — is the whole
    /// point of putting them in the editor now rather than waiting for the real screen.</para>
    /// </summary>
    internal static class HarnessMoments
    {
        /// <summary>
        /// The line for one moment. Numbers are rendered HERE, at the edge, where the locale is known —
        /// which is also why the minute arrives already marked ("68'") rather than the template writing
        /// the apostrophe: an apostrophe after an interpolated value is the exact shape the suffix guard
        /// refuses, and it caught this copy on its first run.
        /// </summary>
        internal static string Line(CareerMoment moment, RunSession session)
        {
            string player = NameOf(moment, session);
            string club = session != null ? session.ManagedClubName : string.Empty;
            string minute = moment.HappenedInAMatch
                ? moment.Minute.ToString(CultureInfo.InvariantCulture) + "'"
                : string.Empty;
            string count = moment.Count != 0
                ? moment.Count.ToString("N0", CultureInfo.InvariantCulture)
                : string.Empty;

            return HarnessCopy.Resolve(
                MomentTextKeys.For(moment.Kind),
                new TextArguments(player, club, minute, count));
        }

        // The journey's own name first: a moment can outlive its subject's time at the club — a sale, a
        // retirement — and by then the squad no longer knows who he was. Asking the roster first is what
        // printed "(left the club)" in the Gate B probe before the journey started carrying a name.
        private static string NameOf(CareerMoment moment, RunSession session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            PlayerJourney journey = session.JourneyOf(moment.Player);
            if (journey != null && !string.IsNullOrEmpty(journey.Name))
            {
                return journey.Name;
            }

            return session.PlayerName(moment.Club, moment.Player);
        }
    }
}
