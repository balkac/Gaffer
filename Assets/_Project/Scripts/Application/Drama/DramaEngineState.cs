using System.Collections.Generic;
using Gaffer.Domain.Drama;

namespace Gaffer.Application.Drama
{
    /// <summary>
    /// Everything <see cref="DramaEngine"/> remembers, as data — for a save (schema v6), which is the only
    /// caller. The engine is stateful per run by design (cooldowns, once-per-run marks and the season
    /// budget are what keep drama rare, GDD §4.7 rule 3), and before v6 none of it survived a reload: load
    /// the file and every cooldown was clear, every once-per-run event was available again and the season
    /// budget was full. That is a save-scum that costs nothing and it is not one the owner asked for.
    /// </summary>
    public sealed class DramaEngineState
    {
        public DramaEngineState(int week, int lastFiredWeek, int firedThisSeason, IReadOnlyList<DramaEventMark> events)
        {
            Week = week;
            LastFiredWeek = lastFiredWeek;
            FiredThisSeason = firedThisSeason;
            Events = events;
        }

        /// <summary>The engine's week counter — the unit cooldowns and the minimum gap are measured in. It
        /// counts ticks, not league rounds, so it is its own number and not derivable from the season.</summary>
        public int Week { get; }

        /// <summary>The week ANY event last fired, for the minimum-gap rule. Far in the past when nothing
        /// has fired yet.</summary>
        public int LastFiredWeek { get; }

        public int FiredThisSeason { get; }

        /// <summary>One mark per event that has fired at least once this run.</summary>
        public IReadOnlyList<DramaEventMark> Events { get; }
    }

    /// <summary>
    /// One event's firing record: the week it last fired, which is BOTH its cooldown clock and its
    /// once-per-run mark. They are one fact rather than two because the engine writes them together — an
    /// event that has ever fired has a last-fired week, and vice versa — so splitting them in the save
    /// would invent a state the engine cannot be in.
    /// </summary>
    public readonly struct DramaEventMark
    {
        public DramaEventMark(DramaEventId dramaEvent, int lastFiredWeek)
        {
            Event = dramaEvent;
            LastFiredWeek = lastFiredWeek;
        }

        public DramaEventId Event { get; }

        public int LastFiredWeek { get; }
    }
}
