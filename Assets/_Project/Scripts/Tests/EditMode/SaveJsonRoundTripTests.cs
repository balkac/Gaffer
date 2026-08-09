using System.Collections.Generic;
using System.IO;
using Gaffer.Application.Serialization;
using Gaffer.Common;
using Gaffer.Domain.Players;
using Gaffer.UserData;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Proves the LEGACY JSON codec still reads and writes the save DTO graph faithfully. It is no longer
    /// the shipped format — <c>BinarySaveSerializer</c> is — but it is the format of every save already on a
    /// player's device, so these tests are what keep that migration path alive
    /// (<c>SaveBinaryFormatTests</c> covers the new one and the hand-off between them).
    /// <para>
    /// Serialize it, parse it back, and confirm every field survives. It runs against Newtonsoft, the same
    /// library the game ships
    /// (<c>com.unity.nuget.newtonsoft-json</c>, pinned to the same version in the test bridge's csproj), so
    /// it verifies the payload the Unity adapter will write without opening the editor: no unsupported
    /// types, no cycles, nulls and ulong intact.
    /// <para>
    /// It goes through <see cref="NewtonsoftJsonSerializer"/> — the SHIPPED adapter with its real
    /// <c>JsonSerializerSettings</c> — rather than <c>JsonConvert</c>, so the stated strictness posture
    /// (null handling, the tolerant missing-member choice of ARCHITECTURE §11) is under test and not just
    /// documented. That became possible when the adapter moved out of the Unity-coupled
    /// <c>Gaffer.Infrastructure</c> into the pure <c>Gaffer.UserData</c> assembly, which the bridge compiles.
    /// </para>
    /// </summary>
    public sealed class SaveJsonRoundTripTests
    {
        private static readonly NewtonsoftJsonSerializer Serializer = new NewtonsoftJsonSerializer();

        /// <summary>Serializes through the shipped adapter, exactly as <c>JsonSaveStore</c> does, and decodes
        /// the bytes back to text — the port is byte-oriented now that the shipped format is binary, so the
        /// JSON codec owns its own UTF-8 encoding.</summary>
        private static string Write(SeasonSaveData data)
        {
            return SaveCodecFixtures.Utf8(SaveCodecFixtures.Write(Serializer, data));
        }

        /// <summary>Reads back through the shipped adapter and unwraps the expected-failure Result.</summary>
        private static SeasonSaveData Read(string json)
        {
            return SaveCodecFixtures.ReadOrFail(Serializer, SaveCodecFixtures.Utf8(json));
        }

        private static SeasonSaveData Sample()
        {
            return SaveCodecFixtures.Sample();
        }

        [Test]
        public void SeasonSaveData_SurvivesAJsonRoundTrip()
        {
            SeasonSaveData original = Sample();

            SeasonSaveData back = Read(Write(original));

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
        public void JsonRoundTrip_CarriesTheV6RunBlock()
        {
            // The legacy codec is also the readable format for a save pasted into a bug report, so the run
            // block has to survive it — and the nested groups are exactly the shape a serializer is most
            // likely to flatten or drop.
            RunSaveData back = Read(Write(Sample())).Run;

            Assert.That(back.OriginalSeed, Is.EqualTo(0x0BADC0DE01UL));
            Assert.That(back.Setup.ManagedClubIndex, Is.EqualTo(1));
            Assert.That(back.Finances.Cash, Is.EqualTo(1_250_000L));
            Assert.That(back.Tactics.Mentality, Is.EqualTo("Attacking"));
            Assert.That(back.Tactics.FormationSlots.Count, Is.EqualTo(11));
            Assert.That(back.Eleven[4], Is.EqualTo(-1), "the empty-slot sentinel is a number, not an absence");
            Assert.That(back.Market[0].Name, Is.EqualTo("Ivo Larkin"));
            Assert.That(back.Morale[0].Points, Is.EqualTo(-3.5).Within(1e-9));
            Assert.That(back.Drama.PendingEventId, Is.EqualTo("night-club-scandal"));
            Assert.That(back.Drama.Events[1].Id, Is.EqualTo("club-takeover"));
        }

        [Test]
        public void JsonRoundTrip_ThenMapper_RebuildsTheLeagueFaithfully()
        {
            SeasonSaveData original = Sample();

            SeasonSaveData parsed = Read(Write(original));
            RestoredSeason restored = new SeasonSaveMapper().Restore(parsed);

            Assert.That(restored.SeasonNumber, Is.EqualTo(4));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Role, Is.EqualTo(PlayerRole.Striker));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].HiddenPotential, Is.EqualTo(91));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Attributes.Pace, Is.EqualTo(34));
            Assert.That(restored.League.Clubs[1].Squad, Is.Null);
        }

        [Test]
        public void Serialize_AppliesTheShippedSettings_IndentedAndNullsOmitted()
        {
            // Formatting.Indented + NullValueHandling.Ignore are the adapter's stated settings; a save is
            // meant to stay human-readable in a bug report, and an absent member is how the tolerant reader
            // takes a default instead of a null.
            string json = Write(Sample());

            Assert.That(json, Does.Contain("\n"), "the shipped settings write indented JSON");
            Assert.That(json, Does.Not.Contain("\"Squad\": null"), "NullValueHandling.Ignore omits the member");
            Assert.That(json, Does.Not.Contain("\"Role\":"), "the retired nullable ordinal is never written");
        }

        [Test]
        public void Deserialize_MemberFromANewerBuild_IsTolerated()
        {
            // ARCHITECTURE §11: save data outlives the build that wrote it, so MissingMemberHandling.Ignore
            // is deliberate — an unknown member must NOT fail the load. This is the assertion that breaks if
            // anyone flips the setting to Error while copying the content reader's posture.
            string json = Write(Sample()).Replace(
                "\"SchemaVersion\":",
                "\"FieldFromANewerBuild\": { \"nested\": [1, 2, 3] },\n  \"SchemaVersion\":");

            SeasonSaveData back = Read(json);

            Assert.That(back.SeasonNumber, Is.EqualTo(4));
            Assert.That(back.Clubs.Count, Is.EqualTo(2));
        }

        [Test]
        public void Deserialize_MemberMissingFromAnOlderBuild_TakesItsDefault()
        {
            string json = Write(Sample()).Replace("\"PlayedRounds\": 3,", string.Empty);

            SeasonSaveData back = Read(json);

            Assert.That(back.PlayedRounds, Is.EqualTo(0), "an absent member takes its default, not a failure");
            Assert.That(back.LeagueName, Is.EqualTo("Round Trip League"));
        }

        [Test]
        public void Deserialize_CorruptText_IsAnExpectedFailure()
        {
            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(Serializer, SaveCodecFixtures.Utf8("{ this is not json"));

            Assert.That(parsed.IsFailure, Is.True, "a torn save is a Result failure, never an exception");
            Assert.That(parsed.Error, Does.StartWith("Could not parse save:"));
        }

        [Test]
        public void Deserialize_EmptyText_IsAnExpectedFailure()
        {
            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(Serializer, new byte[0]);

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Is.EqualTo("Save text did not parse into a save payload."));
        }
    }
}
