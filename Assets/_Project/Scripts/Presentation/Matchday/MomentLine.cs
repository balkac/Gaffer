using System.Globalization;
using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Common.Localization;

namespace Gaffer.Presentation.Matchday
{
    /// <summary>
    /// A recognised <see cref="CareerMoment"/> as the line a player reads.
    ///
    /// <para><b>Why it is here and not in the harness that wrote it first.</b> This logic was proven in
    /// <c>Editor/Harness/HarnessMoments</c> during Gate B, and the match screen needs the same sentence.
    /// Two copies of "how a moment becomes words" would drift, and the first thing to drift would be the
    /// name rule below — which exists because it was already got wrong once.</para>
    ///
    /// <para><b>The split is deliberate.</b> What is shared is what the ARGUMENTS are: which name, which
    /// club, how a minute and a count are written. What is not shared is what to do when a locale has no
    /// words for the key — the harness draws a loud developer marker, the screen shows the key itself, and
    /// neither policy belongs to the other. So <see cref="ArgumentsFor"/> is the seam, and each caller
    /// resolves with its own answer.</para>
    ///
    /// <para>Framework-free, so the headless bridge compiles it (CLAUDE.md test bridge). Adding
    /// <c>using UnityEngine</c> here breaks that build, which is the correct answer.</para>
    /// </summary>
    public static class MomentLine
    {
        /// <summary>
        /// The values a moment's copy interpolates: the player, his club, the minute and the count.
        ///
        /// <para>Numbers are rendered HERE, at the edge, where the locale is known — which is also why the
        /// minute arrives already marked ("68'") rather than the template writing the apostrophe: an
        /// apostrophe after an interpolated value is the exact shape the suffix guard refuses, and it
        /// caught this copy on its first run.</para>
        /// </summary>
        public static TextArguments ArgumentsFor(CareerMoment moment, RunSession session)
        {
            return new TextArguments(
                NameOf(moment, session),
                session != null ? session.ManagedClubName : string.Empty,
                moment.HappenedInAMatch
                    ? moment.Minute.ToString(CultureInfo.InvariantCulture) + "'"
                    : string.Empty,
                moment.Count != 0 ? moment.Count.ToString("N0", CultureInfo.InvariantCulture) : string.Empty);
        }

        /// <summary>The line as a screen shows it — see <see cref="UiWords"/> for why a screen draws the
        /// key rather than throwing.</summary>
        public static string For(CareerMoment moment, RunSession session, LocalizedStrings text)
        {
            return text.Or(MomentTextKeys.For(moment.Kind), ArgumentsFor(moment, session));
        }

        /// <summary>
        /// The line as it reads UNDER the goal that produced it: the short form when the kind has one, the
        /// full line otherwise. The row above already carries the minute and the scorer, so the short form
        /// says only what the goal was — see <see cref="MomentTextKeys.Folded"/>.
        /// </summary>
        public static string Under(CareerMoment moment, RunSession session, LocalizedStrings text)
        {
            string folded = MomentTextKeys.Folded(moment.Kind);
            return folded == null
                ? For(moment, session, text)
                : text.Or(folded, ArgumentsFor(moment, session));
        }

        /// <summary>
        /// The journey's own name first. A moment can outlive its subject's time at the club — a sale, a
        /// retirement — and by then the squad no longer knows who he was. Asking the roster first is what
        /// printed "(left the club)" in the Gate B probe before the journey started carrying a name.
        /// </summary>
        public static string NameOf(CareerMoment moment, RunSession session)
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
