using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Narrative
{
    /// <summary>
    /// One recognised moment in one player's career — a fact the simulation produced, plus the context
    /// that made it worth remembering. This is the unit the journey log stores and the unit the match
    /// narrative and the season recap are written from; it holds no words at all
    /// (NON-NEGOTIABLE #8), only what happened, so the same moment can be told in any language.
    ///
    /// <para>A struct, and a small one: a manager who plays fifty seasons accumulates thousands of these
    /// across his squads, and they are read far more often than they are written (PERFORMANCE §8).</para>
    /// </summary>
    public readonly struct CareerMoment
    {
        public CareerMoment(CareerMomentKind kind, PlayerId player, ClubId club, int season, int round, int minute = NoMinute, long count = 0L)
        {
            Kind = kind;
            Player = player;
            Club = club;
            Season = season;
            Round = round;
            Minute = minute;
            Count = count;
        }

        /// <summary><see cref="Minute"/> for a moment that did not happen inside a match.</summary>
        public const int NoMinute = -1;

        public CareerMomentKind Kind { get; }

        public PlayerId Player { get; }

        /// <summary>The club he was at. A journey follows a player across clubs, so this is not constant.</summary>
        public ClubId Club { get; }

        public int Season { get; }

        /// <summary>The match week, so moments sort within a season and read as a timeline.</summary>
        public int Round { get; }

        /// <summary>The minute, or <see cref="NoMinute"/> for a signing, a sale, a birthday-shaped moment.</summary>
        public int Minute { get; }

        /// <summary>
        /// How many of the thing the kind names, and zero when the kind names no quantity. Goals in the
        /// match for <see cref="CareerMomentKind.Brace"/> and <see cref="CareerMomentKind.Hattrick"/>,
        /// the number reached for the two milestones, the fee for a <see cref="CareerMomentKind.Sale"/>.
        /// A <c>long</c> because of that last one: fees are money, and money in this game is already a
        /// <c>long</c> everywhere else — narrowing it here would silently wrap the one transfer a manager
        /// most wants to remember.
        ///
        /// <para>One shared field rather than a payload type per kind, deliberately: a moment is written
        /// to a save, and every kind that carries a number carries exactly one, so a hierarchy here would
        /// buy nothing but a serializer to keep in step with it. What each kind means by it is stated
        /// above, and the recogniser is the only thing that fills it.</para>
        /// </summary>
        public long Count { get; }

        public bool HappenedInAMatch => Minute != NoMinute;
    }
}
