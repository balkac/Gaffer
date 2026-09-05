using Gaffer.Application.Season;

namespace Gaffer.Presentation.Season
{
    /// <summary>
    /// Where the board's two numbers fall through a league table.
    ///
    /// <para>The zone a position is in is not decided here — <see cref="SeasonEvaluator.Judge"/> is the
    /// rule, and the table asks it, so what the screen paints as promotion is by construction what the
    /// board would call promotion. What IS decided here is where the LINES go, and that is the rule with
    /// the off-by-one in it: a line drawn under the wrong row moves a club into the wrong zone on a table
    /// that otherwise reads perfectly, and nothing throws. Framework-free so <c>dotnet test</c> holds it
    /// (CLAUDE.md test bridge).</para>
    /// </summary>
    public static class TableZones
    {
        /// <summary>The zone the club in this 1-based position is in, as the board would judge it.</summary>
        public static SeasonVerdict Of(int position, BoardTarget target)
        {
            return SeasonEvaluator.Judge(position, target);
        }

        /// <summary>
        /// Whether a line is drawn UNDER this 1-based position: yes exactly where the row below is in a
        /// different zone. Never under the last row — a line separating the table from nothing says
        /// nothing — and never twice for one boundary.
        /// </summary>
        public static bool LineBelow(int position, BoardTarget target, int clubCount)
        {
            return position < clubCount && Of(position, target) != Of(position + 1, target);
        }
    }
}
