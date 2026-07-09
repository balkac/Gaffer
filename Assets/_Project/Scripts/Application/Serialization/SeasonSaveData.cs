using System.Collections.Generic;

namespace Gaffer.Application.Serialization
{
    // One grouped serialization payload (CONVENTIONS §2 DTO exception). Mutable POCOs so any JSON
    // serializer can round-trip them; the domain stays immutable and maps to and from these.

    public sealed class SeasonSaveData
    {
        public int SchemaVersion { get; set; }

        public string LeagueName { get; set; }

        /// <summary>Which season of the run this is — so a save resumes into the right year (multi-season runs).</summary>
        public int SeasonNumber { get; set; }

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

        /// <summary>The full roster, so development and renewal survive across seasons. Null for a
        /// strength-only club (a v2 save, or a harness fixture built from strength alone).</summary>
        public List<PlayerSaveData> Squad { get; set; }
    }

    public sealed class PlayerSaveData
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public string Nationality { get; set; }

        /// <summary>The specific <c>PlayerRole</c> as an int, so the broad position and ratings rebuild from it.</summary>
        public int Role { get; set; }

        public int Age { get; set; }

        public int HiddenPotential { get; set; }

        public AttributesSaveData Attributes { get; set; }
    }

    // The 29 grouped attributes, flat so any JSON serializer round-trips them by name (robust to reordering).
    public sealed class AttributesSaveData
    {
        public byte Finishing { get; set; }
        public byte Technique { get; set; }
        public byte FirstTouch { get; set; }
        public byte Dribbling { get; set; }
        public byte Passing { get; set; }
        public byte Crossing { get; set; }
        public byte Heading { get; set; }
        public byte LongShots { get; set; }
        public byte Marking { get; set; }
        public byte Tackling { get; set; }
        public byte Penalties { get; set; }
        public byte FreeKicks { get; set; }
        public byte Corners { get; set; }
        public byte LongThrows { get; set; }
        public byte Pace { get; set; }
        public byte Acceleration { get; set; }
        public byte Stamina { get; set; }
        public byte Strength { get; set; }
        public byte Agility { get; set; }
        public byte Jumping { get; set; }
        public byte Balance { get; set; }
        public byte Positioning { get; set; }
        public byte Reflexes { get; set; }
        public byte Handling { get; set; }
        public byte AerialReach { get; set; }
        public byte CommandOfArea { get; set; }
        public byte OneOnOnes { get; set; }
        public byte Kicking { get; set; }
        public byte GkPositioning { get; set; }
    }

    public sealed class MatchResultSaveData
    {
        public int Home { get; set; }

        public int Away { get; set; }

        public int HomeGoals { get; set; }

        public int AwayGoals { get; set; }
    }
}
