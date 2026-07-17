using System;

namespace Gaffer.Domain.Traits
{
    /// <summary>
    /// The kinds of match stakes a context-sensitive trait can key on — the pure-data side of the
    /// bridge to the simulation's match context (GDD §4.1: the sim knows what the match means, and
    /// traits fire off that meaning). Flags so one trait can answer to several occasions.
    /// </summary>
    [Flags]
    public enum MatchStakes
    {
        None = 0,
        Derby = 1,
        Rivalry = 2,
        Final = 4,
        TitleDecider = 8,
        RelegationSixPointer = 16,
        BigCrowd = 32,
    }
}
