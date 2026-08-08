using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using Gaffer.Application.Serialization;
using Gaffer.Common;
using Gaffer.Domain.Players;
using Gaffer.UserData;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The contract of the compact binary save container — the format that replaced indented JSON when the
    /// design target became a multi-league world of ~50,000 players (1,119 bytes a player measured at that
    /// size, so a 53 MB save; the container writes ~51, so ~2.4 MB).
    /// <para>
    /// A binary format loses everything JSON gave away free, so each of those has to be bought back and
    /// held here: JSON named every member, so reordering fields was harmless — this is positional, so the
    /// byte layout is PINNED below the way <see cref="PersistedEnumValueTests"/> pins enum values. JSON
    /// failed on a syntax error long before it ran off the end — a binary reader will happily walk past the
    /// end of a truncated file, so EVERY prefix of a valid save is checked to produce a
    /// <see cref="Result"/> failure rather than an exception (UNITY.md §7: never an unhandled crash at
    /// boot). And JSON's numbers were text, so culture and endianness were somebody else's problem — here
    /// they are asserted.
    /// </para>
    /// </summary>
    public sealed class SaveBinaryFormatTests
    {
        private static readonly BinarySaveSerializer Binary = new BinarySaveSerializer();
        private static readonly NewtonsoftJsonSerializer Json = new NewtonsoftJsonSerializer();

        /// <summary>The codec the game wires: binary out, binary or legacy JSON in.</summary>
        private static readonly SaveSerializer Save = new SaveSerializer();

        // ---------------------------------------------------------------- round-trip fidelity

        [Test]
        public void BinaryRoundTrip_CarriesEveryFieldTheJsonSaveDid()
        {
            SeasonSaveData original = SaveCodecFixtures.Sample();

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, original));

            Assert.That(back.SchemaVersion, Is.EqualTo(SeasonSaveData.CurrentVersion));
            Assert.That(back.LeagueName, Is.EqualTo("Round Trip League"));
            Assert.That(back.SeasonNumber, Is.EqualTo(4));
            Assert.That(back.PlayedRounds, Is.EqualTo(3));
            Assert.That(back.MatchSeed, Is.EqualTo(0xDEADBEEFCAFEUL));

            Assert.That(back.Clubs.Count, Is.EqualTo(2));
            Assert.That(back.Clubs[0].Id, Is.EqualTo(0));
            Assert.That(back.Clubs[0].Name, Is.EqualTo("Squad Club"));

            // Exact, not Within: the doubles travel as IEEE-754 bits, so a strength that changed in the last
            // place would mean the format is lossy and the reader is hiding it.
            Assert.That(back.Clubs[0].Attack, Is.EqualTo(61.5));
            Assert.That(back.Clubs[0].Midfield, Is.EqualTo(58.25));
            Assert.That(back.Clubs[0].Defence, Is.EqualTo(60.0));

            Assert.That(back.Clubs[1].Squad, Is.Null, "a strength-only club keeps a NULL squad, not an empty one");

            PlayerSaveData player = back.Clubs[0].Squad[0];
            Assert.That(player.Id, Is.EqualTo(7));
            Assert.That(player.Name, Is.EqualTo("Cy Vale"));
            Assert.That(player.Nationality, Is.EqualTo("Spain"));
            Assert.That(player.RoleName, Is.EqualTo("Striker"), "the role survives as a NAME, not a number");
            Assert.That(player.Role, Is.Null, "the retired v4 ordinal is not even encodable in the container");
            Assert.That(player.Age, Is.EqualTo(19));
            Assert.That(player.HiddenPotential, Is.EqualTo(91));
            Assert.That(player.Traits, Is.EqualTo(new List<string> { "derby-beast", "glass-man" }));

            AssertAttributesAreTheDistinctFixture(player.Attributes);

            Assert.That(back.Results.Count, Is.EqualTo(1));
            Assert.That(back.Results[0].Home, Is.EqualTo(0));
            Assert.That(back.Results[0].Away, Is.EqualTo(1));
            Assert.That(back.Results[0].HomeGoals, Is.EqualTo(2));
            Assert.That(back.Results[0].AwayGoals, Is.EqualTo(1));
        }

        [Test]
        public void BinaryRoundTrip_ThenMapper_RebuildsTheLeagueFaithfully()
        {
            SeasonSaveData parsed = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample()));

            RestoredSeason restored = new SeasonSaveMapper().Restore(parsed);

            Assert.That(restored.SeasonNumber, Is.EqualTo(4));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Role, Is.EqualTo(PlayerRole.Striker));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].HiddenPotential, Is.EqualTo(91));
            Assert.That(restored.League.Clubs[0].Squad.Players[0].Attributes.Pace, Is.EqualTo(34));
            Assert.That(restored.League.Clubs[1].Squad, Is.Null);
        }

        [Test]
        public void BinaryRoundTrip_DistinguishesNullFromEmpty()
        {
            // The mapper branches on a null squad, and a trait-less player is not the same document as one
            // whose trait list was dropped. A format that collapsed either would restore a wrong league.
            var data = new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                LeagueName = string.Empty,
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData { Id = 0, Name = null, Squad = null },
                    new ClubSaveData { Id = 1, Name = string.Empty, Squad = new List<PlayerSaveData>() },
                    new ClubSaveData
                    {
                        Id = 2, Name = "Mixed",
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData { Id = 0, Name = "No Traits", RoleName = "Striker", Traits = null, Attributes = null },
                            new PlayerSaveData { Id = 1, Name = "Empty Traits", RoleName = "Striker", Traits = new List<string>(), Attributes = new AttributesSaveData() },
                        },
                    },
                },
            };

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, data));

            Assert.That(back.LeagueName, Is.EqualTo(string.Empty), "an empty string is not null");
            Assert.That(back.Clubs[0].Name, Is.Null);
            Assert.That(back.Clubs[0].Squad, Is.Null);
            Assert.That(back.Clubs[1].Name, Is.EqualTo(string.Empty));
            Assert.That(back.Clubs[1].Squad, Is.Not.Null.And.Empty, "an empty squad is not a missing one");
            Assert.That(back.Clubs[2].Squad[0].Traits, Is.Null);
            Assert.That(back.Clubs[2].Squad[0].Attributes, Is.Null, "a null attribute block round-trips as null");
            Assert.That(back.Clubs[2].Squad[1].Traits, Is.Not.Null.And.Empty);
            Assert.That(back.Clubs[2].Squad[1].Attributes, Is.Not.Null);
        }

        [Test]
        public void BinaryRoundTrip_ExtremeIntegers_SurviveExactly()
        {
            // The container writes an int as its unchecked uint reinterpretation rather than zigzag, which
            // is the cheap choice for the non-negative data this format actually carries. This is the test
            // that the choice is still LOSSLESS for everything else.
            var data = new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                SeasonNumber = int.MinValue,
                PlayedRounds = int.MaxValue,
                MatchSeed = ulong.MaxValue,
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData
                    {
                        Id = -1, Attack = double.MaxValue, Midfield = double.Epsilon, Defence = -0.0,
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData { Id = int.MaxValue, RoleName = "Goalkeeper", Age = 0, HiddenPotential = 255 },
                        },
                    },
                },
            };

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, data));

            Assert.That(back.SeasonNumber, Is.EqualTo(int.MinValue));
            Assert.That(back.PlayedRounds, Is.EqualTo(int.MaxValue));
            Assert.That(back.MatchSeed, Is.EqualTo(ulong.MaxValue));
            Assert.That(back.Clubs[0].Id, Is.EqualTo(-1));
            Assert.That(back.Clubs[0].Attack, Is.EqualTo(double.MaxValue));
            Assert.That(back.Clubs[0].Midfield, Is.EqualTo(double.Epsilon));
            Assert.That(back.Clubs[0].Squad[0].Id, Is.EqualTo(int.MaxValue));
            Assert.That(back.Clubs[0].Squad[0].HiddenPotential, Is.EqualTo(255));
        }

        [Test]
        public void BinaryRoundTrip_NonAsciiText_SurvivesAsUtf8()
        {
            var data = new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                LeagueName = "Süper Lig",
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData
                    {
                        Id = 0, Name = "Beşiktaş",
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData { Id = 0, Name = "Gökhan Çığdem", Nationality = "Türkiye", RoleName = "CentreBack" },
                        },
                    },
                },
            };

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, data));

            Assert.That(back.LeagueName, Is.EqualTo("Süper Lig"));
            Assert.That(back.Clubs[0].Name, Is.EqualTo("Beşiktaş"));
            Assert.That(back.Clubs[0].Squad[0].Name, Is.EqualTo("Gökhan Çığdem"));
            Assert.That(back.Clubs[0].Squad[0].Nationality, Is.EqualTo("Türkiye"));
        }

        // ---------------------------------------------------------------- the pinned byte layout

        [Test]
        public void Header_IsTheMagicAndTheContainerVersion()
        {
            byte[] bytes = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());

            // 'G','F','S','V' then the container version as a little-endian ushort. This is the whole
            // detection contract: change it and every save on every device becomes unrecognisable.
            Assert.That(bytes[0], Is.EqualTo(0x47));
            Assert.That(bytes[1], Is.EqualTo(0x46));
            Assert.That(bytes[2], Is.EqualTo(0x53));
            Assert.That(bytes[3], Is.EqualTo(0x56));
            Assert.That(bytes[4], Is.EqualTo(1), "container version, low byte first");
            Assert.That(bytes[5], Is.EqualTo(0), "container version, high byte — little-endian, on every device");
            Assert.That(BinarySaveSerializer.CurrentContainerVersion, Is.EqualTo(1),
                "The container version moved. Add the branch that reads the old layout, then update this test.");
        }

        [Test]
        public void AttributeBlock_IsTwentyNineBytesInThePinnedOrder()
        {
            // The one place the compact format trades away JSON's by-name safety. The fixture puts a
            // distinct value in every slot, so this asserts the ORDER and not merely the count: swapping two
            // lines in the writer would silently swap two attributes on every player in every save that
            // already exists, and nothing at runtime would ever notice.
            byte[] bytes = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());

            int start = IndexOf(bytes, new byte[] { 20, 21, 22, 23, 24 });
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "the attribute block is written as raw bytes");
            Assert.That(bytes[start - 1], Is.EqualTo(1), "a present attribute block is preceded by its 1 marker");

            var expected = new byte[]
            {
                20, 21, 22, 23, 24, 25, 26, 27, 28, 29,
                30, 31, 32, 33, 34, 35, 36, 37, 38, 39,
                40, 41, 42, 43, 44, 45, 46, 47, 48,
            };
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.That(bytes[start + i], Is.EqualTo(expected[i]),
                    "attribute byte " + i + " moved. Put it back and APPEND instead — the old position is "
                    + "already in every save on every device.");
            }
        }

        [Test]
        public void Roles_ArePersistedByName_NotByOrdinal()
        {
            // The bug this project already fixed once (schema v4 -> v5) was a raw ordinal; a compact format
            // is exactly the pressure that would reintroduce it. It does not: the literal name is in the
            // bytes, and the ordinal 11 that "Striker" used to be written as is not what identifies him.
            byte[] bytes = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());

            Assert.That(IndexOf(bytes, SaveCodecFixtures.Utf8("Striker")), Is.GreaterThanOrEqualTo(0),
                "the save must contain the role's NAME");
            Assert.That(IndexOf(bytes, SaveCodecFixtures.Utf8("Spain")), Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void RepeatedText_IsWrittenOnceAndReferencedThereafter()
        {
            // The mechanism that lets the format keep enums BY NAME and still hit ~51 bytes a player: the
            // second Netherlands international costs a byte, not the word again. If this regresses, the size
            // guard below is the thing that fails, so this test names the reason.
            var player = new PlayerSaveData
            {
                Id = 1,
                Name = "One",
                Nationality = "Netherlands",
                RoleName = "AttackingMidfield",
                Traits = new List<string> { "big-game-player" },
                Attributes = new AttributesSaveData(),
            };
            var twin = new PlayerSaveData
            {
                Id = 2,
                Name = "Two",
                Nationality = "Netherlands",
                RoleName = "AttackingMidfield",
                Traits = new List<string> { "big-game-player" },
                Attributes = new AttributesSaveData(),
            };

            byte[] alone = SaveCodecFixtures.Write(Binary, DocumentOf(player));
            byte[] pair = SaveCodecFixtures.Write(Binary, DocumentOf(player, twin));

            Assert.That(Count(pair, SaveCodecFixtures.Utf8("Netherlands")), Is.EqualTo(1),
                "a repeated nationality is written once and referenced thereafter");
            Assert.That(Count(pair, SaveCodecFixtures.Utf8("AttackingMidfield")), Is.EqualTo(1),
                "and so is a role NAME — which is what makes persisting enums by name affordable here");
            Assert.That(Count(pair, SaveCodecFixtures.Utf8("big-game-player")), Is.EqualTo(1));

            // Adding the twin costs his own id, name, age, potential and attribute block — not another
            // 43 bytes of repeated text.
            Assert.That(pair.Length - alone.Length, Is.LessThan(45));
        }

        // ---------------------------------------------------------------- endianness and culture

        [Test]
        public void Output_IsByteIdenticalUnderAHostileCulture()
        {
            // A save written on one device must read on another, including one in a Turkish locale — the
            // classic place a formatted number or a case-insensitive compare changes meaning. The container
            // never formats a number as text, so this asserts the whole file is unchanged, not just that it
            // parses.
            SeasonSaveData data = SaveCodecFixtures.Sample();
            byte[] invariant;
            byte[] hostile;

            CultureInfo previous = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                invariant = SaveCodecFixtures.Write(Binary, data);

                Thread.CurrentThread.CurrentCulture = new CultureInfo("tr-TR");
                hostile = SaveCodecFixtures.Write(Binary, data);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }

            Assert.That(hostile, Is.EqualTo(invariant), "the save's bytes must not depend on the device's culture");

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, hostile);
            Assert.That(back.Clubs[0].Midfield, Is.EqualTo(58.25), "a double read under tr-TR is still exact");
        }

        [Test]
        public void FixedWidthNumbers_AreLittleEndianOnEveryHost()
        {
            // The seed is the one fixed-width integer in the format, so it is the one that proves the byte
            // order is WRITTEN rather than inherited from the machine. 0x0102030405060708 little-endian is
            // 08 07 06 05 04 03 02 01.
            var data = new SeasonSaveData { SchemaVersion = 5, LeagueName = null, MatchSeed = 0x0102030405060708UL };

            byte[] bytes = SaveCodecFixtures.Write(Binary, data);
            int seed = IndexOf(bytes, new byte[] { 0x08, 0x07, 0x06, 0x05, 0x04, 0x03, 0x02, 0x01 });

            Assert.That(seed, Is.GreaterThanOrEqualTo(0), "fixed-width numbers are little-endian, low byte first");
        }

        // ---------------------------------------------------------------- truncation and corruption

        [Test]
        public void EveryTruncationOfAValidSave_IsAResultFailureAndNeverAnException()
        {
            // The failure mode a binary format introduces and JSON did not have: the OS can kill a
            // backgrounding app mid-write, so a save that stops at ANY byte is a file the loader will meet.
            // Checking every prefix is the only way to know none of them walks off the end of the buffer
            // (UNITY.md §7).
            byte[] complete = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());

            for (int length = 0; length < complete.Length; length++)
            {
                var truncated = new byte[length];
                Array.Copy(complete, truncated, length);

                Result<SeasonSaveData> parsed = default;
                Assert.DoesNotThrow(
                    () => parsed = SaveCodecFixtures.Read(Binary, truncated),
                    "a save truncated to " + length + " bytes threw instead of failing");
                Assert.That(parsed.IsFailure, Is.True, "a save truncated to " + length + " bytes must not parse");
                Assert.That(parsed.Error, Is.Not.Null.And.Not.Empty, "a failure must say what went wrong");
            }
        }

        [Test]
        public void EverySingleByteCorruption_IsHandledWithoutAnException()
        {
            // A save is a file on a device a player can edit, and flash storage flips bits. Whether a
            // damaged byte is caught or happens to decode into a nonsense-but-well-formed document is not
            // the point; the point is that no byte, anywhere, can produce an unhandled throw on the boot
            // path — including the allocation blow-up a believed length would cause.
            byte[] complete = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());
            byte[] injections = { 0x00, 0x7F, 0x80, 0xFF };

            for (int index = 0; index < complete.Length; index++)
            {
                foreach (byte injection in injections)
                {
                    var damaged = (byte[])complete.Clone();
                    damaged[index] = injection;

                    Assert.DoesNotThrow(
                        () => SaveCodecFixtures.Read(Binary, damaged),
                        "byte " + index + " set to 0x" + injection.ToString("X2") + " threw");
                }
            }
        }

        [Test]
        public void ATextFile_IsRejectedAsNotABinarySave()
        {
            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(Binary, SaveCodecFixtures.Utf8("{ \"SchemaVersion\": 5 }"));

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Is.EqualTo("Save is not a Gaffer binary save."));
        }

        [Test]
        public void AnEmptyFile_IsAResultFailure()
        {
            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(Binary, new byte[0]);

            Assert.That(parsed.IsFailure, Is.True);
        }

        [Test]
        public void AStringPointerToTextTheFileNeverDefined_IsRejected()
        {
            // Hand-forged rather than corrupted, so the failure is the one being named: an interned-string
            // reference the writer could not have produced.
            byte[] bytes = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());
            int nationality = IndexOf(bytes, SaveCodecFixtures.Utf8("Spain"));
            bytes[nationality - 2] = 0x7F; // was "a new string follows"; now "pool entry 125".

            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(Binary, bytes);

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Does.Contain("before defining it"));
        }

        // ---------------------------------------------------------------- the container acceptance policy

        [Test]
        public void AContainerFromANewerBuild_IsRefusedByNumber()
        {
            // ARCHITECTURE §11's acceptance policy, at the container level: a layout this build does not
            // know is refused whole rather than half-read into something that looks like a season. The
            // document-schema half of the policy still belongs to SaveMigrator.
            byte[] bytes = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());
            bytes[4] = 99;

            Result<SeasonSaveData> parsed = SaveCodecFixtures.Read(Binary, bytes);

            Assert.That(parsed.IsFailure, Is.True);
            Assert.That(parsed.Error, Does.Contain("99").And.Contains("1..1"));
        }

        [Test]
        public void AContainerVersionOfZero_IsRefused()
        {
            byte[] bytes = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());
            bytes[4] = 0;

            Assert.That(SaveCodecFixtures.Read(Binary, bytes).IsFailure, Is.True);
        }

        [Test]
        public void TrailingBytesPastTheDocument_AreIgnored()
        {
            // The stated tolerant posture, made concrete: a later container may append sections, and an
            // older build meeting one must take the fields it knows rather than refuse the save.
            byte[] complete = SaveCodecFixtures.Write(Binary, SaveCodecFixtures.Sample());
            var padded = new byte[complete.Length + 16];
            Array.Copy(complete, padded, complete.Length);

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, padded);

            Assert.That(back.Clubs.Count, Is.EqualTo(2));
        }

        [Test]
        public void TheDocumentSchemaVersion_TravelsInsideTheContainer_AndStillMigrates()
        {
            // The two versions are separate facts: the container describes the ENCODING, the schema
            // describes the FIELDS. A v4 document encoded in a v1 container is a legal file, and the
            // migration chain — not the codec — is what lifts it.
            var v4 = new SeasonSaveData
            {
                SchemaVersion = 4,
                LeagueName = "V4 in a binary container",
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData
                    {
                        Id = 0, Name = "Old FC",
                        Squad = new List<PlayerSaveData>
                        {
                            new PlayerSaveData { Id = 1, Name = "Old Player", RoleName = "Striker", Age = 24, HiddenPotential = 70 },
                        },
                    },
                },
            };

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, v4));
            Assert.That(back.SchemaVersion, Is.EqualTo(4), "the codec reports the document's schema, it does not rewrite it");

            Result<SeasonSaveData> migrated = new SaveMigrator().Migrate(back);
            Assert.That(migrated.IsSuccess, Is.True, migrated.Error);
            Assert.That(migrated.Value.SchemaVersion, Is.EqualTo(SeasonSaveData.CurrentVersion));
        }

        // ---------------------------------------------------------------- the v5 JSON migration path

        [Test]
        public void SaveSerializer_WritesTheBinaryContainer()
        {
            byte[] bytes = SaveCodecFixtures.Write(Save, SaveCodecFixtures.Sample());

            Assert.That(bytes[0], Is.EqualTo(0x47), "the shipped codec writes binary, never JSON");
            Assert.That(bytes.Length, Is.LessThan(600), "and it is the compact one");
        }

        [Test]
        public void SaveSerializer_StillReadsAV5JsonSave()
        {
            // The migration decision under test: saves the owner already has are v5 JSON, and they are
            // accepted on load rather than converted by a one-shot pass. This is a GENUINE v5 payload —
            // written by the JSON adapter with its shipped settings, exactly as it sits in a file on a
            // device.
            byte[] legacy = SaveCodecFixtures.Write(Json, SaveCodecFixtures.Sample());
            Assert.That(legacy[0], Is.EqualTo((byte)'{'), "the fixture really is the old text format");

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Save, legacy);

            Assert.That(back.SchemaVersion, Is.EqualTo(5));
            Assert.That(back.LeagueName, Is.EqualTo("Round Trip League"));
            Assert.That(back.Clubs[0].Squad[0].RoleName, Is.EqualTo("Striker"));
            Assert.That(back.Clubs[1].Squad, Is.Null);
            AssertAttributesAreTheDistinctFixture(back.Clubs[0].Squad[0].Attributes);
        }

        [Test]
        public void AV5JsonSave_ReadThenWritten_BecomesABinarySaveWithTheSameContent()
        {
            // The whole upgrade path in one test: an old file loads, the next ordinary save writes it in the
            // new format, and nothing is lost on the way through. No conversion tool is involved — that is
            // the point of accepting both on load.
            byte[] legacy = SaveCodecFixtures.Write(Json, SaveCodecFixtures.Sample());

            SeasonSaveData loaded = SaveCodecFixtures.ReadOrFail(Save, legacy);
            byte[] upgraded = SaveCodecFixtures.Write(Save, loaded);
            SeasonSaveData reloaded = SaveCodecFixtures.ReadOrFail(Save, upgraded);

            Assert.That(upgraded[0], Is.EqualTo(0x47));
            Assert.That(upgraded.Length, Is.LessThan(legacy.Length / 2), "the upgrade is the point: it must be far smaller");

            Assert.That(reloaded.LeagueName, Is.EqualTo("Round Trip League"));
            Assert.That(reloaded.SeasonNumber, Is.EqualTo(4));
            Assert.That(reloaded.MatchSeed, Is.EqualTo(0xDEADBEEFCAFEUL));
            Assert.That(reloaded.Clubs[0].Squad[0].RoleName, Is.EqualTo("Striker"));
            Assert.That(reloaded.Clubs[0].Squad[0].Traits, Is.EqualTo(new List<string> { "derby-beast", "glass-man" }));
            AssertAttributesAreTheDistinctFixture(reloaded.Clubs[0].Squad[0].Attributes);
        }

        [Test]
        public void SaveSerializer_ATornFileOfEitherFormat_IsAResultFailure()
        {
            Assert.That(SaveCodecFixtures.Read(Save, SaveCodecFixtures.Utf8("{ this is not json")).IsFailure, Is.True);
            Assert.That(SaveCodecFixtures.Read(Save, new byte[] { 0x47, 0x46, 0x53, 0x56 }).IsFailure, Is.True);
            Assert.That(SaveCodecFixtures.Read(Save, new byte[0]).IsFailure, Is.True);
        }

        // ---------------------------------------------------------------- the size guard

        /// <summary>
        /// The ceiling this change exists to hold, and the reason it is a test rather than a note: a save
        /// re-inflates one innocuous field at a time, and the day it matters is the day it is a 50,000-player
        /// world on a mobile heap. Measured today at ~51 bytes a player, so 64 leaves about a quarter of
        /// headroom — enough for a genuinely-needed small field without a format change, and low enough that
        /// anything careless (a per-player string, a spelled-out enum with no interning, a float that should
        /// have been a byte) trips it. At 64 the design target still fits in ~3.2 MB.
        /// </summary>
        private const int MaxBytesPerPlayer = 64;

        [Test]
        public void AWorldSizedSave_StaysUnderTheBytesPerPlayerCeiling()
        {
            const int players = 5000;
            SeasonSaveData world = WorldOf(players);

            byte[] bytes = SaveCodecFixtures.Write(Binary, world);
            double perPlayer = bytes.Length / (double)players;

            Assert.That(perPlayer, Is.LessThan(MaxBytesPerPlayer),
                "The save grew to " + perPlayer.ToString("F1") + " bytes a player. At the design target of "
                + "50,000 players that is " + (perPlayer * 50000 / 1048576.0).ToString("F1") + " MB, and the "
                + "atomic write means a second transient copy of it on a non-compacting heap. Either the new "
                + "field earns its bytes and this ceiling moves deliberately, or it should be interned, "
                + "packed, or derived on load instead of stored.");
        }

        [Test]
        public void AWorldSizedSave_RoundTripsEveryPlayer()
        {
            const int players = 5000;
            SeasonSaveData world = WorldOf(players);

            SeasonSaveData back = SaveCodecFixtures.ReadOrFail(Binary, SaveCodecFixtures.Write(Binary, world));

            int seen = 0;
            for (int c = 0; c < world.Clubs.Count; c++)
            {
                List<PlayerSaveData> before = world.Clubs[c].Squad;
                List<PlayerSaveData> after = back.Clubs[c].Squad;
                Assert.That(after.Count, Is.EqualTo(before.Count));
                for (int p = 0; p < before.Count; p++)
                {
                    Assert.That(after[p].Id, Is.EqualTo(before[p].Id));
                    Assert.That(after[p].Name, Is.EqualTo(before[p].Name));
                    Assert.That(after[p].Nationality, Is.EqualTo(before[p].Nationality));
                    Assert.That(after[p].RoleName, Is.EqualTo(before[p].RoleName));
                    Assert.That(after[p].Age, Is.EqualTo(before[p].Age));
                    Assert.That(after[p].HiddenPotential, Is.EqualTo(before[p].HiddenPotential));
                    Assert.That(after[p].Attributes.Finishing, Is.EqualTo(before[p].Attributes.Finishing));
                    Assert.That(after[p].Attributes.GkPositioning, Is.EqualTo(before[p].Attributes.GkPositioning));
                    Assert.That(after[p].Traits, Is.EqualTo(before[p].Traits));
                    seen++;
                }
            }

            Assert.That(seen, Is.EqualTo(players));
        }

        // ---------------------------------------------------------------- the file store, unchanged

        [Test]
        public void JsonSaveStore_SavesAndLoadsTheBinaryFormat_AndKeepsItsBackupProtocol()
        {
            // Task requirement six: the atomic write protocol (temp -> flush -> Replace, .bak recovery) is
            // format-agnostic and must keep working untouched. Saving twice creates the .bak; destroying the
            // current file must then fall back to it rather than losing the run.
            string directory = Path.Combine(Path.GetTempPath(), "gaffer-binary-save-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "season.sav");
            var store = new JsonSaveStore(Save, new SaveMigrator());

            try
            {
                SeasonSaveData first = SaveCodecFixtures.Sample();
                Assert.That(store.Save(path, first).IsSuccess, Is.True);

                SeasonSaveData second = SaveCodecFixtures.Sample();
                second.LeagueName = "Second Save";
                Assert.That(store.Save(path, second).IsSuccess, Is.True);
                Assert.That(File.Exists(path + ".bak"), Is.True, "the replace step keeps the previous save");
                Assert.That(File.ReadAllBytes(path)[0], Is.EqualTo(0x47), "the store wrote the binary container");

                Result<SeasonSaveData> loaded = store.Load(path);
                Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
                Assert.That(loaded.Value.LeagueName, Is.EqualTo("Second Save"));
                Assert.That(loaded.Value.SchemaVersion, Is.EqualTo(SeasonSaveData.CurrentVersion));

                // Now tear the current file the way a kill mid-write would.
                byte[] torn = File.ReadAllBytes(path);
                File.WriteAllBytes(path, new byte[] { torn[0], torn[1], torn[2], torn[3], torn[4], torn[5], torn[6] });

                Result<SeasonSaveData> recovered = store.Load(path);
                Assert.That(recovered.IsSuccess, Is.True, recovered.Error);
                Assert.That(recovered.Value.LeagueName, Is.EqualTo("Round Trip League"), "a torn save falls back to the .bak");
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        [Test]
        public void JsonSaveStore_LoadsAV5JsonFileWrittenByAnOlderBuild()
        {
            // The owner's existing saves, on disk, read by the shipped store with no conversion step.
            string directory = Path.Combine(Path.GetTempPath(), "gaffer-legacy-save-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "season.sav");

            try
            {
                Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, SaveCodecFixtures.Write(Json, SaveCodecFixtures.Sample()));

                var store = new JsonSaveStore(Save, new SaveMigrator());
                Result<SeasonSaveData> loaded = store.Load(path);

                Assert.That(loaded.IsSuccess, Is.True, loaded.Error);
                Assert.That(loaded.Value.Clubs[0].Squad[0].RoleName, Is.EqualTo("Striker"));

                // And the next ordinary save upgrades the file in place.
                Assert.That(store.Save(path, loaded.Value).IsSuccess, Is.True);
                Assert.That(File.ReadAllBytes(path)[0], Is.EqualTo(0x47));
                Assert.That(store.Load(path).IsSuccess, Is.True);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        // ---------------------------------------------------------------- helpers

        private static void AssertAttributesAreTheDistinctFixture(AttributesSaveData a)
        {
            Assert.That(a, Is.Not.Null);
            Assert.That(a.Finishing, Is.EqualTo(20));
            Assert.That(a.Technique, Is.EqualTo(21));
            Assert.That(a.FirstTouch, Is.EqualTo(22));
            Assert.That(a.Dribbling, Is.EqualTo(23));
            Assert.That(a.Passing, Is.EqualTo(24));
            Assert.That(a.Crossing, Is.EqualTo(25));
            Assert.That(a.Heading, Is.EqualTo(26));
            Assert.That(a.LongShots, Is.EqualTo(27));
            Assert.That(a.Marking, Is.EqualTo(28));
            Assert.That(a.Tackling, Is.EqualTo(29));
            Assert.That(a.Penalties, Is.EqualTo(30));
            Assert.That(a.FreeKicks, Is.EqualTo(31));
            Assert.That(a.Corners, Is.EqualTo(32));
            Assert.That(a.LongThrows, Is.EqualTo(33));
            Assert.That(a.Pace, Is.EqualTo(34));
            Assert.That(a.Acceleration, Is.EqualTo(35));
            Assert.That(a.Stamina, Is.EqualTo(36));
            Assert.That(a.Strength, Is.EqualTo(37));
            Assert.That(a.Agility, Is.EqualTo(38));
            Assert.That(a.Jumping, Is.EqualTo(39));
            Assert.That(a.Balance, Is.EqualTo(40));
            Assert.That(a.Positioning, Is.EqualTo(41));
            Assert.That(a.Reflexes, Is.EqualTo(42));
            Assert.That(a.Handling, Is.EqualTo(43));
            Assert.That(a.AerialReach, Is.EqualTo(44));
            Assert.That(a.CommandOfArea, Is.EqualTo(45));
            Assert.That(a.OneOnOnes, Is.EqualTo(46));
            Assert.That(a.Kicking, Is.EqualTo(47));
            Assert.That(a.GkPositioning, Is.EqualTo(48));
        }

        /// <summary>A one-club document holding exactly the given players.</summary>
        private static SeasonSaveData DocumentOf(params PlayerSaveData[] players)
        {
            return new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                LeagueName = "Interning",
                Clubs = new List<ClubSaveData>
                {
                    new ClubSaveData { Id = 0, Name = "Pool FC", Squad = new List<PlayerSaveData>(players) },
                },
            };
        }

        private static int Count(byte[] haystack, byte[] needle)
        {
            int found = 0;
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    found++;
                }
            }

            return found;
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }

        private static readonly string[] FirstNames =
        {
            "Ada", "Ben", "Cy", "Dan", "Eli", "Finn", "Gus", "Hal", "Ivo", "Jon",
            "Kai", "Levi", "Milo", "Noah", "Otto", "Piet", "Quin", "Rui", "Sami", "Tom",
        };

        private static readonly string[] LastNames =
        {
            "Keeper", "Wolf", "Vale", "Ridge", "Moss", "Fell", "Crane", "Hart", "Stone", "Frost",
            "Marsh", "Brook", "Thorne", "Quill", "Vance", "Ashby", "Rook", "Doyle", "Faber", "Nash",
            "Odell", "Pryor", "Vega", "Sorel", "Larkin", "Mendez", "Kovac", "Ibsen", "Duarte", "Halvorsen",
        };

        private static readonly string[] Nations =
        {
            "England", "Spain", "France", "Germany", "Italy", "Netherlands", "Portugal", "Brazil",
            "Argentina", "Wales", "Scotland", "Belgium", "Croatia", "Denmark", "Sweden", "Norway",
            "Turkey", "Japan", "Nigeria", "Senegal",
        };

        private static readonly string[] TraitIds =
        {
            "derby-beast", "glass-man", "big-game-player", "slow-starter", "leader", "hot-head",
        };

        /// <summary>
        /// A save shaped like the real thing at scale — 25-player squads, names drawn from pools, a
        /// nationality and a role and a couple of traits per player, a full result list. Deterministic
        /// (fixed seed) so the size guard measures the format and not the fixture.
        /// </summary>
        private static SeasonSaveData WorldOf(int playerCount)
        {
            var rng = new Random(12345);
            const int perClub = 25;
            int clubCount = Math.Max(1, (playerCount + perClub - 1) / perClub);

            var data = new SeasonSaveData
            {
                SchemaVersion = SeasonSaveData.CurrentVersion,
                LeagueName = "Guard League",
                SeasonNumber = 3,
                PlayedRounds = 19,
                MatchSeed = 0xDEADBEEFCAFEUL,
                Clubs = new List<ClubSaveData>(clubCount),
            };

            int id = 0;
            for (int c = 0; c < clubCount; c++)
            {
                var squad = new List<PlayerSaveData>(perClub);
                for (int p = 0; p < perClub && id < playerCount; p++, id++)
                {
                    var traits = new List<string>();
                    int traitCount = rng.Next(0, 3);
                    for (int t = 0; t < traitCount; t++)
                    {
                        traits.Add(TraitIds[rng.Next(TraitIds.Length)]);
                    }

                    squad.Add(new PlayerSaveData
                    {
                        Id = id,
                        Name = FirstNames[rng.Next(FirstNames.Length)] + " " + LastNames[rng.Next(LastNames.Length)],
                        Nationality = Nations[rng.Next(Nations.Length)],
                        RoleName = PersistedPlayerRole.ToName((PlayerRole)rng.Next(12)),
                        Age = rng.Next(16, 38),
                        HiddenPotential = rng.Next(40, 100),
                        Attributes = RandomAttributes(rng),
                        Traits = traits,
                    });
                }

                data.Clubs.Add(new ClubSaveData
                {
                    Id = c,
                    Name = "Club " + c,
                    Attack = 55.5 + (c % 20),
                    Midfield = 56.25 + (c % 20),
                    Defence = 57.75 + (c % 20),
                    Squad = squad,
                });
            }

            for (int r = 0; r < 380; r++)
            {
                data.Results.Add(new MatchResultSaveData
                {
                    Home = r % clubCount,
                    Away = (r + 1) % clubCount,
                    HomeGoals = r % 4,
                    AwayGoals = r % 3,
                });
            }

            return data;
        }

        private static AttributesSaveData RandomAttributes(Random rng)
        {
            return new AttributesSaveData
            {
                Finishing = Roll(rng),
                Technique = Roll(rng),
                FirstTouch = Roll(rng),
                Dribbling = Roll(rng),
                Passing = Roll(rng),
                Crossing = Roll(rng),
                Heading = Roll(rng),
                LongShots = Roll(rng),
                Marking = Roll(rng),
                Tackling = Roll(rng),
                Penalties = Roll(rng),
                FreeKicks = Roll(rng),
                Corners = Roll(rng),
                LongThrows = Roll(rng),
                Pace = Roll(rng),
                Acceleration = Roll(rng),
                Stamina = Roll(rng),
                Strength = Roll(rng),
                Agility = Roll(rng),
                Jumping = Roll(rng),
                Balance = Roll(rng),
                Positioning = Roll(rng),
                Reflexes = Roll(rng),
                Handling = Roll(rng),
                AerialReach = Roll(rng),
                CommandOfArea = Roll(rng),
                OneOnOnes = Roll(rng),
                Kicking = Roll(rng),
                GkPositioning = Roll(rng),
            };
        }

        private static byte Roll(Random rng)
        {
            return (byte)rng.Next(1, 20);
        }
    }
}
