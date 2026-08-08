using System.Collections.Generic;
using System.IO;
using System.Text;
using Gaffer.Application.Serialization;
using Gaffer.Common;

namespace Gaffer.Tests
{
    /// <summary>
    /// Shared scaffolding for the save-codec tests: the one sample document both codecs are held to, and the
    /// stream plumbing the <see cref="ISerializer"/> port needs now that it is byte-oriented. It lives in
    /// one place so the JSON tests and the binary tests exercise the SAME payload — that is what makes
    /// "the binary format carries everything the JSON one did" a checkable claim rather than two independent
    /// fixtures that happen to agree.
    /// </summary>
    internal static class SaveCodecFixtures
    {
        /// <summary>Serializes through a codec exactly as <c>JsonSaveStore</c> does, and hands back the
        /// bytes that would have hit the file.</summary>
        internal static byte[] Write(ISerializer serializer, SeasonSaveData data)
        {
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(data, stream);
                return stream.ToArray();
            }
        }

        /// <summary>Reads bytes back through a codec, leaving the Result wrapped so a test can assert on a
        /// failure.</summary>
        internal static Result<SeasonSaveData> Read(ISerializer serializer, byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes, writable: false))
            {
                return serializer.Deserialize(stream);
            }
        }

        /// <summary>Reads bytes back and fails the test if the codec did not accept them.</summary>
        internal static SeasonSaveData ReadOrFail(ISerializer serializer, byte[] bytes)
        {
            Result<SeasonSaveData> parsed = Read(serializer, bytes);
            NUnit.Framework.Assert.That(parsed.IsSuccess, NUnit.Framework.Is.True, parsed.Error);
            return parsed.Value;
        }

        internal static byte[] Utf8(string text)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text);
        }

        internal static string Utf8(byte[] bytes)
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes);
        }

        /// <summary>
        /// The reference document: one club with a full squad and one strength-only club, a player with a
        /// distinct value in every one of the 29 attributes, traits, a non-trivial season seed, and a
        /// played result. Every shape the format has to carry is in here exactly once — a null squad, a
        /// null-vs-empty distinction, a role that must survive BY NAME.
        /// </summary>
        internal static SeasonSaveData Sample()
        {
            return new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                LeagueName = "Round Trip League",
                SeasonNumber = 4,
                MatchSeed = 0xDEADBEEFCAFEUL,
                PlayedRounds = 3,
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData
                    {
                        Id = 0, Name = "Squad Club", Attack = 61.5, Midfield = 58.25, Defence = 60.0,
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData
                            {
                                Id = 7, Name = "Cy Vale", Nationality = "Spain", RoleName = "Striker", Age = 19, HiddenPotential = 91,
                                Attributes = DistinctAttributes(),
                                Traits = new List<string> { "derby-beast", "glass-man" },
                            },
                        },
                    },
                    new ClubSaveData { Id = 1, Name = "Strength Club", Attack = 55, Midfield = 55, Defence = 55, Squad = null },
                },
                Results = new List<MatchResultSaveData>
                {
                    new MatchResultSaveData { Home = 0, Away = 1, HomeGoals = 2, AwayGoals = 1 },
                },
            };
        }

        /// <summary>A different value in all 29 slots, so a codec that swaps two attributes cannot pass.
        /// The values are also the byte sequence the binary format's attribute block is pinned to.</summary>
        internal static AttributesSaveData DistinctAttributes()
        {
            return new AttributesSaveData
            {
                Finishing = 20,
                Technique = 21,
                FirstTouch = 22,
                Dribbling = 23,
                Passing = 24,
                Crossing = 25,
                Heading = 26,
                LongShots = 27,
                Marking = 28,
                Tackling = 29,
                Penalties = 30,
                FreeKicks = 31,
                Corners = 32,
                LongThrows = 33,
                Pace = 34,
                Acceleration = 35,
                Stamina = 36,
                Strength = 37,
                Agility = 38,
                Jumping = 39,
                Balance = 40,
                Positioning = 41,
                Reflexes = 42,
                Handling = 43,
                AerialReach = 44,
                CommandOfArea = 45,
                OneOnOnes = 46,
                Kicking = 47,
                GkPositioning = 48,
            };
        }
    }
}
