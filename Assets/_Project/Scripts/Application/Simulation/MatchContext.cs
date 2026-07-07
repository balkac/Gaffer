namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// The match's stakes, threaded into the simulation as the bridge that binds character to the
    /// match (TDD §6, NON-NEGOTIABLE): the strength step reads it so context-sensitive traits raise
    /// or lower a player's effective attributes, and narrative reads it for emphasis. Immutable
    /// data — the sim decides how to weigh it.
    /// </summary>
    public readonly struct MatchContext
    {
        public MatchContext(MatchImportance importance, int crowdSize, bool isTitleDecider, bool isRivalry)
        {
            Importance = importance;
            CrowdSize = crowdSize;
            IsTitleDecider = isTitleDecider;
            IsRivalry = isRivalry;
        }

        public MatchImportance Importance { get; }

        public int CrowdSize { get; }

        public bool IsTitleDecider { get; }

        public bool IsRivalry { get; }
    }
}
