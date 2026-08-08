using System;
using System.Collections.Generic;
using System.IO;
using Gaffer.Application.Serialization;
using Gaffer.Common;

namespace Gaffer.UserData
{
    /// <summary>
    /// The shipped save codec: the same <see cref="SeasonSaveData"/> the JSON adapter carried, written as a
    /// compact binary container. Indented JSON spent 1,070 bytes on a player; this spends about 55, which is
    /// what makes the design target — a multi-league world of ~50,000 players — a ~3 MB save instead of a
    /// 51 MB one. That is not a nicety on mobile: the atomic write protocol means a second transient copy of
    /// whatever the save costs, and Unity's Boehm GC is non-compacting (PERFORMANCE §10), so a 51 MB
    /// allocation permanently raises the heap floor of the process that made it.
    ///
    /// <para>
    /// THE FORMAT (container version 1). Little-endian throughout, UTF-8 without a BOM, no number ever
    /// formatted as text — see <see cref="SaveBinaryWriter"/> for why both are stated rather than inherited.
    /// <c>varint</c> is unsigned LEB128; <c>int</c> is a varint of the value's unchecked <c>uint</c>
    /// reinterpretation; <c>string</c> is <c>0</c> for null else <c>byteLength+1</c> then the bytes;
    /// <c>interned</c> is <c>0</c> for null, <c>1</c> then <c>byteLength</c> then the bytes for a string the
    /// file has not carried yet, else <c>2 + poolIndex</c>.
    /// </para>
    /// <code>
    /// magic            4 bytes  'G','F','S','V'
    /// containerVersion u16      little-endian
    /// schemaVersion    varint   the DOCUMENT's schema (SeasonSaveData.SchemaVersion) — a separate fact
    /// leagueName       string
    /// seasonNumber     int
    /// playedRounds     int
    /// matchSeed        u64      little-endian, fixed width (a random seed does not compress)
    /// clubCount        varint
    ///   per club:
    ///     id           int
    ///     name         string
    ///     attack       f64      IEEE-754 bits, little-endian
    ///     midfield     f64
    ///     defence      f64
    ///     squadTag     varint   0 = no roster (strength-only club), else playerCount + 1
    ///       per player:
    ///         id              int
    ///         name            string
    ///         nationality     interned
    ///         roleName        interned    the PlayerRole member NAME, exactly as v5 JSON wrote it
    ///         age             int
    ///         hiddenPotential int
    ///         hasAttributes   1 byte      0 = null, 1 = the 29 bytes follow
    ///         attributes      29 bytes    fixed order, pinned by SaveBinaryFormatTests
    ///         traitsTag       varint      0 = null list, else traitCount + 1
    ///         traits          interned x traitCount
    /// resultCount      varint
    ///   per result: home int, away int, homeGoals int, awayGoals int
    /// </code>
    ///
    /// <para>
    /// ENUMS ARE STILL PERSISTED BY NAME. A player's role is the literal <c>"Goalkeeper"</c>, not an
    /// ordinal — the compact format pays for it once per distinct name via the string pool and one byte per
    /// player thereafter, so there was no size argument for reintroducing the raw ordinals schema v5 exists
    /// to retire (UNITY.md §7, CONVENTIONS §6). What IS positional is the 29-byte attribute block and the
    /// field order above; that is the trade a compact format makes, and it is pinned byte-for-byte by
    /// <c>SaveBinaryFormatTests</c> the way <c>PersistedEnumValueTests</c> pins the enum values, so a
    /// reordered field fails loudly instead of silently rewriting everyone's saves.
    /// </para>
    ///
    /// <para>
    /// TWO VERSIONS, TWO FACTS (ARCHITECTURE §11). The CONTAINER version above describes the ENCODING and is
    /// this class's business: <see cref="CurrentContainerVersion"/> is what it writes,
    /// <see cref="MinimumSupportedContainerVersion"/>..<see cref="CurrentContainerVersion"/> is what it
    /// accepts. The SCHEMA version inside describes the DOCUMENT — which fields exist — and stays exactly
    /// where it was: <see cref="SeasonSaveData.CurrentVersion"/> for the fact, <see cref="SaveMigrator"/>
    /// for the acceptance policy and the migration chain. They are deliberately not merged, because
    /// re-encoding the same fields and adding a field are independent changes and collapsing them is the
    /// one-number trap §11 names.
    /// </para>
    ///
    /// <para>
    /// STRICTNESS POSTURE. This is player save data, so the posture is still the tolerant one — but the
    /// mechanism changes and that is worth stating, because it is what a reader will otherwise assume wrong.
    /// JSON got tolerance free: an unknown member was skipped and a missing one defaulted. A positional
    /// binary record cannot skip what it cannot see, so tolerance is bought explicitly instead: an ADDITIVE
    /// field bumps the container version and the decode branches on it (older containers simply do not read
    /// the new field and it takes its default), while trailing bytes past the last record are IGNORED rather
    /// than rejected, so a future container may append sections. What is not tolerated is a container
    /// version this build does not know, which is refused by number instead of being half-read.
    /// </para>
    /// </summary>
    public sealed class BinarySaveSerializer : ISerializer
    {
        /// <summary>The container version this build WRITES — a fact about the documents it produces.</summary>
        public const ushort CurrentContainerVersion = 1;

        /// <summary>The oldest container version this build will READ. With
        /// <see cref="CurrentContainerVersion"/> this is the acceptance policy, the container-level twin of
        /// the one <see cref="SaveMigrator"/> owns for the document schema (ARCHITECTURE §11).</summary>
        public const ushort MinimumSupportedContainerVersion = 1;

        /// <summary>How many attribute bytes a player record carries. Fixed, not written into the file: the
        /// count is part of the container version, and a change to it is a new container version.</summary>
        internal const int AttributeCount = 29;

        /// <summary>Sized-list capacity ceiling. A count read from the file is checked against the format's
        /// limit, but even a legal-looking one must not be trusted to pre-size a list — a 4-million-element
        /// capacity from a damaged byte would allocate tens of MB before the truncated file failed. Capping
        /// the initial capacity lets a genuine 50,000-player world grow amortised while a corrupt count
        /// costs nothing.</summary>
        private const int MaxPresizedCapacity = 4096;

        /// <summary>The one-byte "no attributes" marker, kept as a field so writing a null block does not
        /// allocate.</summary>
        private static readonly byte[] AbsentAttributes = { 0 };

        public void Serialize(SeasonSaveData data, Stream stream)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var writer = new SaveBinaryWriter(stream);
            writer.WriteMagicAndContainerVersion(CurrentContainerVersion);

            writer.WriteInt32(data.SchemaVersion);
            writer.WriteString(data.LeagueName);
            writer.WriteInt32(data.SeasonNumber);
            writer.WriteInt32(data.PlayedRounds);
            writer.WriteUInt64(data.MatchSeed);

            // One scratch block for the whole document, threaded down to the attribute writer: a fresh
            // 30-byte array per player is 50,000 allocations for a world-sized save, which is the kind of
            // per-element garbage PERFORMANCE §4 rules out. Passing it rather than holding it in a field
            // keeps this serializer stateless and therefore safe to share.
            byte[] attributeBuffer = new byte[1 + AttributeCount];

            List<ClubSaveData> clubs = data.Clubs;
            writer.WriteVarUInt32(clubs == null ? 0u : (uint)clubs.Count);
            if (clubs != null)
            {
                for (int i = 0; i < clubs.Count; i++)
                {
                    WriteClub(writer, clubs[i], attributeBuffer);
                }
            }

            List<MatchResultSaveData> results = data.Results;
            writer.WriteVarUInt32(results == null ? 0u : (uint)results.Count);
            if (results != null)
            {
                for (int i = 0; i < results.Count; i++)
                {
                    MatchResultSaveData result = results[i];
                    writer.WriteInt32(result.Home);
                    writer.WriteInt32(result.Away);
                    writer.WriteInt32(result.HomeGoals);
                    writer.WriteInt32(result.AwayGoals);
                }
            }
        }

        public Result<SeasonSaveData> Deserialize(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }

            var reader = new SaveBinaryReader(stream);
            try
            {
                if (!reader.TryReadHeader(out ushort containerVersion))
                {
                    return Result<SeasonSaveData>.Failure("Save is not a Gaffer binary save.");
                }

                // Acceptance policy, checked before a single record is decoded: a container this build does
                // not know may lay its records out differently, so it is refused by number rather than
                // half-read into something that looks like a season.
                if (containerVersion < MinimumSupportedContainerVersion || containerVersion > CurrentContainerVersion)
                {
                    return Result<SeasonSaveData>.Failure(
                        "Save container version " + containerVersion + " is outside the supported range "
                        + MinimumSupportedContainerVersion + ".." + CurrentContainerVersion + ".");
                }

                return Result<SeasonSaveData>.Success(ReadDocument(reader));
            }
            catch (MalformedSaveException e)
            {
                // The one place the decoder's bounds checks become an outcome instead of an exception. A
                // torn save is expected (the OS can kill a backgrounding app mid-write) and the caller has a
                // defined fallback — the .bak, then a fresh run (UNITY.md §7).
                return Result<SeasonSaveData>.Failure("Could not parse save: " + e.Message);
            }
        }

        private static void WriteClub(SaveBinaryWriter writer, ClubSaveData club, byte[] attributeBuffer)
        {
            writer.WriteInt32(club.Id);
            writer.WriteString(club.Name);
            writer.WriteDouble(club.Attack);
            writer.WriteDouble(club.Midfield);
            writer.WriteDouble(club.Defence);

            List<PlayerSaveData> squad = club.Squad;
            if (squad == null)
            {
                // A strength-only club (an older save, or a harness fixture built from strength alone) must
                // come back with a NULL squad, not an empty one — the mapper branches on exactly that.
                writer.WriteVarUInt32(SaveBinaryPrimitives.NullTag);
                return;
            }

            writer.WriteVarUInt32((uint)squad.Count + 1);
            for (int i = 0; i < squad.Count; i++)
            {
                WritePlayer(writer, squad[i], attributeBuffer);
            }
        }

        private static void WritePlayer(SaveBinaryWriter writer, PlayerSaveData player, byte[] attributeBuffer)
        {
            writer.WriteInt32(player.Id);
            writer.WriteString(player.Name);
            writer.WriteInternedString(player.Nationality);
            writer.WriteInternedString(player.RoleName);
            writer.WriteInt32(player.Age);
            writer.WriteInt32(player.HiddenPotential);

            // PlayerSaveData.Role — the retired v4 ordinal — is deliberately NOT encodable here. It exists
            // on the DTO only so SaveMigrator can read a pre-v5 JSON document, and a v5+ document must not
            // carry it; the JSON adapter drops it via NullValueHandling.Ignore and this one has no field for
            // it at all. A player read back from a binary save therefore always has Role == null.
            WriteAttributes(writer, player.Attributes, attributeBuffer);

            List<string> traits = player.Traits;
            if (traits == null)
            {
                writer.WriteVarUInt32(SaveBinaryPrimitives.NullTag);
                return;
            }

            writer.WriteVarUInt32((uint)traits.Count + 1);
            for (int i = 0; i < traits.Count; i++)
            {
                writer.WriteInternedString(traits[i]);
            }
        }

        /// <summary>
        /// The 29 attributes as 29 raw bytes, in the order below. THIS ORDER IS WIRE FORMAT: unlike JSON,
        /// which named every member and so survived reordering, a positional block means moving a line here
        /// silently swaps two attributes on every player in every existing save. It is pinned byte-for-byte
        /// by <c>SaveBinaryFormatTests</c>, which is what makes such an edit fail loudly instead. Append
        /// only, and appending is a new container version.
        /// </summary>
        private static void WriteAttributes(SaveBinaryWriter writer, AttributesSaveData attributes, byte[] buffer)
        {
            if (attributes == null)
            {
                writer.WriteBytes(AbsentAttributes, 0, 1);
                return;
            }

            buffer[0] = 1;
            buffer[1] = attributes.Finishing;
            buffer[2] = attributes.Technique;
            buffer[3] = attributes.FirstTouch;
            buffer[4] = attributes.Dribbling;
            buffer[5] = attributes.Passing;
            buffer[6] = attributes.Crossing;
            buffer[7] = attributes.Heading;
            buffer[8] = attributes.LongShots;
            buffer[9] = attributes.Marking;
            buffer[10] = attributes.Tackling;
            buffer[11] = attributes.Penalties;
            buffer[12] = attributes.FreeKicks;
            buffer[13] = attributes.Corners;
            buffer[14] = attributes.LongThrows;
            buffer[15] = attributes.Pace;
            buffer[16] = attributes.Acceleration;
            buffer[17] = attributes.Stamina;
            buffer[18] = attributes.Strength;
            buffer[19] = attributes.Agility;
            buffer[20] = attributes.Jumping;
            buffer[21] = attributes.Balance;
            buffer[22] = attributes.Positioning;
            buffer[23] = attributes.Reflexes;
            buffer[24] = attributes.Handling;
            buffer[25] = attributes.AerialReach;
            buffer[26] = attributes.CommandOfArea;
            buffer[27] = attributes.OneOnOnes;
            buffer[28] = attributes.Kicking;
            buffer[29] = attributes.GkPositioning;
            writer.WriteBytes(buffer, 0, 1 + AttributeCount);
        }

        /// <summary>Reads the document. The object initialisers below depend on C#'s left-to-right member
        /// evaluation: every member is a READ from the stream, so their source order IS the wire order and
        /// reordering a line silently reads the wrong field.</summary>
        private static SeasonSaveData ReadDocument(SaveBinaryReader reader)
        {
            var data = new SeasonSaveData
            {
                SchemaVersion = reader.ReadInt32(),
                LeagueName = reader.ReadString(),
                SeasonNumber = reader.ReadInt32(),
                PlayedRounds = reader.ReadInt32(),
                MatchSeed = reader.ReadUInt64(),
            };

            byte[] attributeBuffer = new byte[AttributeCount];

            int clubCount = reader.ReadLength("clubs");
            data.Clubs = new List<ClubSaveData>(Math.Min(clubCount, MaxPresizedCapacity));
            for (int i = 0; i < clubCount; i++)
            {
                data.Clubs.Add(ReadClub(reader, attributeBuffer));
            }

            int resultCount = reader.ReadLength("results");
            data.Results = new List<MatchResultSaveData>(Math.Min(resultCount, MaxPresizedCapacity));
            for (int i = 0; i < resultCount; i++)
            {
                data.Results.Add(new MatchResultSaveData
                {
                    Home = reader.ReadInt32(),
                    Away = reader.ReadInt32(),
                    HomeGoals = reader.ReadInt32(),
                    AwayGoals = reader.ReadInt32(),
                });
            }

            // Trailing bytes are ignored on purpose — see the strictness posture on the class: a later
            // container may append sections, and an older build reading one must take the fields it knows
            // rather than refuse the file.
            return data;
        }

        private static ClubSaveData ReadClub(SaveBinaryReader reader, byte[] attributeBuffer)
        {
            var club = new ClubSaveData
            {
                Id = reader.ReadInt32(),
                Name = reader.ReadString(),
                Attack = reader.ReadDouble(),
                Midfield = reader.ReadDouble(),
                Defence = reader.ReadDouble(),
            };

            uint squadTag = (uint)reader.ReadLength("players");
            if (squadTag == SaveBinaryPrimitives.NullTag)
            {
                club.Squad = null;
                return club;
            }

            int playerCount = (int)(squadTag - 1);
            club.Squad = new List<PlayerSaveData>(Math.Min(playerCount, MaxPresizedCapacity));
            for (int i = 0; i < playerCount; i++)
            {
                club.Squad.Add(ReadPlayer(reader, attributeBuffer));
            }

            return club;
        }

        private static PlayerSaveData ReadPlayer(SaveBinaryReader reader, byte[] attributeBuffer)
        {
            var player = new PlayerSaveData
            {
                Id = reader.ReadInt32(),
                Name = reader.ReadString(),
                Nationality = reader.ReadInternedString(),
                RoleName = reader.ReadInternedString(),
                Age = reader.ReadInt32(),
                HiddenPotential = reader.ReadInt32(),
                Attributes = ReadAttributes(reader, attributeBuffer),
            };

            uint traitsTag = (uint)reader.ReadLength("traits");
            if (traitsTag == SaveBinaryPrimitives.NullTag)
            {
                player.Traits = null;
                return player;
            }

            int traitCount = (int)(traitsTag - 1);
            player.Traits = new List<string>(Math.Min(traitCount, MaxPresizedCapacity));
            for (int i = 0; i < traitCount; i++)
            {
                player.Traits.Add(reader.ReadInternedString());
            }

            return player;
        }

        private static AttributesSaveData ReadAttributes(SaveBinaryReader reader, byte[] buffer)
        {
            if (reader.ReadByte() == 0)
            {
                return null;
            }

            reader.ReadBytes(buffer, 0, AttributeCount);
            return new AttributesSaveData
            {
                Finishing = buffer[0],
                Technique = buffer[1],
                FirstTouch = buffer[2],
                Dribbling = buffer[3],
                Passing = buffer[4],
                Crossing = buffer[5],
                Heading = buffer[6],
                LongShots = buffer[7],
                Marking = buffer[8],
                Tackling = buffer[9],
                Penalties = buffer[10],
                FreeKicks = buffer[11],
                Corners = buffer[12],
                LongThrows = buffer[13],
                Pace = buffer[14],
                Acceleration = buffer[15],
                Stamina = buffer[16],
                Strength = buffer[17],
                Agility = buffer[18],
                Jumping = buffer[19],
                Balance = buffer[20],
                Positioning = buffer[21],
                Reflexes = buffer[22],
                Handling = buffer[23],
                AerialReach = buffer[24],
                CommandOfArea = buffer[25],
                OneOnOnes = buffer[26],
                Kicking = buffer[27],
                GkPositioning = buffer[28],
            };
        }
    }
}
