using System;
using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Maps between the live season and its serializable snapshot. Capture records the clubs (with their
    /// full squads, so development and renewal survive across seasons), the season number, the played
    /// results, the rounds done, and the season seed; Restore rebuilds the league and replays the results
    /// so the resumed season continues deterministically — remaining fixtures are seeded from
    /// <see cref="SeasonSaveData.MatchSeed"/>, so they reproduce an uninterrupted run exactly. A club with
    /// no roster (a strength-only harness fixture, or an older v2 save) round-trips as strength only.
    /// </summary>
    public sealed class SeasonSaveMapper
    {
        public SeasonSaveData Capture(League league, LeagueSeason season, ulong matchSeed, int seasonNumber)
        {
            var data = new SeasonSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                LeagueName = league.Name,
                SeasonNumber = seasonNumber,
                PlayedRounds = season.CurrentRound,
                MatchSeed = matchSeed,
            };

            foreach (Club club in league.Clubs)
            {
                data.Clubs.Add(new ClubSaveData
                {
                    Id = club.Id.Value,
                    Name = club.Name,
                    Attack = club.Strength.Attack,
                    Midfield = club.Strength.Midfield,
                    Defence = club.Strength.Defence,
                    Squad = CaptureSquad(club.Squad),
                });
            }

            foreach (MatchResult result in season.PlayedResults)
            {
                data.Results.Add(new MatchResultSaveData
                {
                    Home = result.Home.Value,
                    Away = result.Away.Value,
                    HomeGoals = result.HomeGoals,
                    AwayGoals = result.AwayGoals,
                });
            }

            return data;
        }

        public RestoredSeason Restore(SeasonSaveData data)
        {
            return Restore(data, null);
        }

        public RestoredSeason Restore(SeasonSaveData data, TraitCatalog traits)
        {
            return Restore(data, traits, null, null);
        }

        /// <summary>Restores against specific config-asset settings so the rebuilt season's strength step
        /// resolves traits, tactics balance, and morale balance the same way the live one did; nulls fall
        /// back to the built-in defaults.</summary>
        public RestoredSeason Restore(SeasonSaveData data, TraitCatalog traits, TacticsSettings tacticsSettings, Gaffer.Application.Drama.MoraleSettings moraleSettings)
        {
            var clubs = new List<Club>(data.Clubs.Count);
            foreach (ClubSaveData club in data.Clubs)
            {
                var strength = new TeamStrength(club.Attack, club.Midfield, club.Defence);
                Squad squad = RestoreSquad(club.Squad);
                clubs.Add(squad == null
                    ? new Club(new ClubId(club.Id), club.Name, strength)
                    : new Club(new ClubId(club.Id), club.Name, squad, strength));
            }

            var league = new League(data.LeagueName, clubs);

            var results = new List<MatchResult>(data.Results.Count);
            foreach (MatchResultSaveData result in data.Results)
            {
                // Per-goal events are not persisted (score is enough to rebuild the table); the
                // narrative layer (Faz 5) will decide what match detail a save must keep.
                results.Add(new MatchResult(new ClubId(result.Home), new ClubId(result.Away), result.HomeGoals, result.AwayGoals, Array.Empty<MatchEvent>()));
            }

            LeagueSeason season = LeagueSeason.Restore(league, data.PlayedRounds, results, traits, tacticsSettings, moraleSettings);
            return new RestoredSeason(league, season, data.SeasonNumber);
        }

        private static List<PlayerSaveData> CaptureSquad(Squad squad)
        {
            if (squad == null)
            {
                return null;
            }

            var players = new List<PlayerSaveData>(squad.Players.Count);
            foreach (Player player in squad.Players)
            {
                players.Add(new PlayerSaveData
                {
                    Id = player.Id.Value,
                    Name = player.Name,
                    Nationality = player.Nationality,
                    Role = (int)player.Role,
                    Age = player.Age,
                    HiddenPotential = player.HiddenPotential,
                    Attributes = ToData(player.Attributes),
                    Traits = CaptureTraits(player),
                });
            }

            return players;
        }

        private static Squad RestoreSquad(List<PlayerSaveData> saved)
        {
            if (saved == null)
            {
                return null;
            }

            var players = new List<Player>(saved.Count);
            foreach (PlayerSaveData p in saved)
            {
                players.Add(new Player(
                    new PlayerId(p.Id), p.Name, p.Nationality, (PlayerRole)p.Role, p.Age,
                    FromData(p.Attributes), (byte)p.HiddenPotential, RestoreTraits(p.Traits)));
            }

            return new Squad(players);
        }

        private static List<string> CaptureTraits(Player player)
        {
            if (player.Traits.Count == 0)
            {
                return null;
            }

            var slugs = new List<string>(player.Traits.Count);
            foreach (TraitId id in player.Traits)
            {
                slugs.Add(id.Value);
            }

            return slugs;
        }

        // Null (a v3 save, or a trait-less player) restores as no traits; unknown slugs are kept — the
        // catalog resolves ids on use and ignores what it does not define.
        private static IReadOnlyList<TraitId> RestoreTraits(List<string> slugs)
        {
            if (slugs == null || slugs.Count == 0)
            {
                return null;
            }

            var ids = new List<TraitId>(slugs.Count);
            foreach (string slug in slugs)
            {
                ids.Add(new TraitId(slug));
            }

            return ids;
        }

        private static AttributesSaveData ToData(Attributes a)
        {
            return new AttributesSaveData
            {
                Finishing = a.Finishing, Technique = a.Technique, FirstTouch = a.FirstTouch, Dribbling = a.Dribbling,
                Passing = a.Passing, Crossing = a.Crossing, Heading = a.Heading, LongShots = a.LongShots,
                Marking = a.Marking, Tackling = a.Tackling, Penalties = a.Penalties, FreeKicks = a.FreeKicks,
                Corners = a.Corners, LongThrows = a.LongThrows, Pace = a.Pace, Acceleration = a.Acceleration,
                Stamina = a.Stamina, Strength = a.Strength, Agility = a.Agility, Jumping = a.Jumping,
                Balance = a.Balance, Positioning = a.Positioning, Reflexes = a.Reflexes, Handling = a.Handling,
                AerialReach = a.AerialReach, CommandOfArea = a.CommandOfArea, OneOnOnes = a.OneOnOnes,
                Kicking = a.Kicking, GkPositioning = a.GkPositioning,
            };
        }

        private static Attributes FromData(AttributesSaveData d)
        {
            return new Attributes
            {
                Finishing = d.Finishing, Technique = d.Technique, FirstTouch = d.FirstTouch, Dribbling = d.Dribbling,
                Passing = d.Passing, Crossing = d.Crossing, Heading = d.Heading, LongShots = d.LongShots,
                Marking = d.Marking, Tackling = d.Tackling, Penalties = d.Penalties, FreeKicks = d.FreeKicks,
                Corners = d.Corners, LongThrows = d.LongThrows, Pace = d.Pace, Acceleration = d.Acceleration,
                Stamina = d.Stamina, Strength = d.Strength, Agility = d.Agility, Jumping = d.Jumping,
                Balance = d.Balance, Positioning = d.Positioning, Reflexes = d.Reflexes, Handling = d.Handling,
                AerialReach = d.AerialReach, CommandOfArea = d.CommandOfArea, OneOnOnes = d.OneOnOnes,
                Kicking = d.Kicking, GkPositioning = d.GkPositioning,
            };
        }
    }
}
