using System.Collections.Generic;

namespace Gaffer.Application.Serialization
{
    // One grouped serialization payload (CONVENTIONS §2 DTO exception). Mutable POCOs so any JSON
    // serializer can round-trip them; the domain stays immutable and maps to and from these.

    public sealed class SeasonSaveData
    {
        public int SchemaVersion { get; set; }

        public string LeagueName { get; set; }

        public List<ClubSaveData> Clubs { get; set; } = new List<ClubSaveData>();

        public int PlayedRounds { get; set; }

        public List<MatchResultSaveData> Results { get; set; } = new List<MatchResultSaveData>();

        /// <summary>The fixed season seed each match is derived from — enough to reproduce every fixture.</summary>
        public ulong MatchSeed { get; set; }
    }

    public sealed class ClubSaveData
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public double Attack { get; set; }

        public double Midfield { get; set; }

        public double Defence { get; set; }
    }

    public sealed class MatchResultSaveData
    {
        public int Home { get; set; }

        public int Away { get; set; }

        public int HomeGoals { get; set; }

        public int AwayGoals { get; set; }
    }
}
