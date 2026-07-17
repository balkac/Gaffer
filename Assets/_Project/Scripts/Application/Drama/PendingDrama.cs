using Gaffer.Domain.Drama;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// An event the engine has raised and now waits on — the decision moment (GDD §4.7 rule 2). The
    /// budget is already spent when this is returned (rarity is enforced at the raise, not at the
    /// answer), and the week's context is kept so team-wide effects know who was in the room.
    /// </summary>
    public sealed class PendingDrama
    {
        public PendingDrama(DramaEvent dramaEvent, Player subject, DramaWeekContext context)
        {
            Event = dramaEvent;
            Subject = subject;
            Context = context;
        }

        public DramaEvent Event { get; }

        /// <summary>The player it happened to; null for a club-level event.</summary>
        public Player Subject { get; }

        public DramaWeekContext Context { get; }
    }
}
