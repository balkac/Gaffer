using Gaffer.Application.Narrative;
using Gaffer.Application.Run;
using Gaffer.Presentation.Matchday;

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
        /// <summary>The line for one moment, in whichever locale <c>HarnessCopy</c> is set to.</summary>
        internal static string Line(CareerMoment moment, RunSession session)
        {
            // The arguments are shared with the match screen (Presentation.Matchday.MomentLine) so the two
            // cannot disagree about which name a moment carries; the RESOLUTION stays here, because a
            // window wants a loud developer marker for a missing key and a screen does not.
            return HarnessCopy.Resolve(
                MomentTextKeys.For(moment.Kind),
                MomentLine.ArgumentsFor(moment, session));
        }
    }
}
