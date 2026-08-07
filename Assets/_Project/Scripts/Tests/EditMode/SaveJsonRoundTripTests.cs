using System.Collections.Generic;
using Gaffer.Application.Serialization;
using Gaffer.Domain.Players;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Proves the save DTO graph is JSON-clean end to end — serialize it, parse it back, and confirm every
    /// field survives. It runs against Newtonsoft, the same library the game ships
    /// (<c>com.unity.nuget.newtonsoft-json</c>, pinned to the same version in the test bridge's csproj), so
    /// it verifies the payload the Unity adapter will write without opening the editor: no unsupported
    /// types, no cycles, nulls and ulong intact.
    /// <para>
    /// KNOWN GAP: it calls <c>JsonConvert</c> directly rather than <c>NewtonsoftJsonSerializer</c>, so the
    /// adapter's own <c>JsonSerializerSettings</c> (null handling, the stated missing-member posture) are
    /// NOT exercised here — this file asserts the DTO shape, not the shipped settings. It cannot be fixed by
    /// moving the test: the adapter lives in <c>Gaffer.Infrastructure</c>, which both the
    /// <c>Gaffer.Tests</c> asmdef and <c>tests/Gaffer.Tests.csproj</c> deliberately exclude so the pure core
    /// stays provably framework-free. Covering the settings needs a Unity PlayMode/EditMode assembly that
    /// may reference Infrastructure; that is a test-infrastructure decision, not something to smuggle in by
    /// widening this bridge.
    /// </para>
    /// </summary>
    public sealed class SaveJsonRoundTripTests
    {
        private static SeasonSaveData Sample()
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
                                Attributes = new AttributesSaveData
                                {
                                    Finishing = 20, Technique = 21, FirstTouch = 22, Dribbling = 23, Passing = 24,
                                    Crossing = 25, Heading = 26, LongShots = 27, Marking = 28, Tackling = 29,
                                    Penalties = 30, FreeKicks = 31, Corners = 32, LongThrows = 33, Pace = 34,
                                    Acceleration = 35, Stamina = 36, Strength = 37, Agility = 38, Jumping = 39,
                                    Balance = 40, Positioning = 41, Reflexes = 42, Handling = 43, AerialReach = 44,
                                    CommandOfArea = 45, OneOnOnes = 46, Kicking = 47, GkPositioning = 48,
                                },
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

        [Test]
        public void SeasonSaveData_SurvivesAJsonRoundTrip()
        {
            SeasonSaveData original = Sample();

            string json = JsonConvert.SerializeObject(original);
            SeasonSaveData back = JsonConvert.DeserializeObject<SeasonSaveData>(json);

            Assert.That(back.SchemaVersion, Is.EqualTo(original.SchemaVersion));
            Assert.That(back.LeagueName, Is.EqualTo(original.LeagueName));
            Assert.That(back.SeasonNumber, Is.EqualTo(4));
            Assert.That(back.MatchSeed, Is.EqualTo(0xDEADBEEFCAFEUL));
            Assert.That(back.PlayedRounds, Is.EqualTo(3));
            Assert.That(back.Clubs.Count, Is.EqualTo(2));
            Assert.That(back.Results.Count, Is.EqualTo(1));
            Assert.That(back.Clubs[1].Squad, Is.Null, "a strength-only club keeps a null squad through JSON");

            PlayerSaveData player = back.Clubs[0].Squad[0];
            Assert.That(player.Name, Is.EqualTo("Cy Vale"));
            Assert.That(player.RoleName, Is.EqualTo("Striker"), "the role survives as a name, not a number");
            Assert.That(player.Role, Is.Null, "the retired v4 ordinal field stays absent");
            Assert.That(player.HiddenPotential, Is.EqualTo(91));
            Assert.That(player.Attributes.Finishing, Is.EqualTo(20));
            Assert.That(player.Attributes.GkPositioning, Is.EqualTo(48));
            Assert.That(player.Traits, Is.EqualTo(new List<string> { "derby-beast", "glass-man" }));
        }

        [Test]
        public void JsonRoundTrip_ThenMapper_RebuildsTheLeagueFaithfully()
        {
            SeasonSaveData original = Sample();

            string json = JsonConvert.SerializeObject(original);
            SeasonSaveData parsed = JsonConvert.DeserializeObject<SeasonSaveData>(json);
            RestoredSeason restored = new SeasonSaveMapper().Restore(parsed);

            Assert.That(restored.SeasonNumber, Is.EqualTo(4));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Role, Is.EqualTo(PlayerRole.Striker));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].HiddenPotential, Is.EqualTo(91));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Attributes.Pace, Is.EqualTo(34));
            Assert.That(restored.League.Clubs[1].Squad, Is.Null);
        }
    }
}
