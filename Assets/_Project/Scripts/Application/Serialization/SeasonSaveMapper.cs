using System;
using System.Collections.Generic;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;

namespace Gaffer.Application.Serialization
{
    /// <summary>
    /// Maps between the live season and its serializable snapshot. Capture records the clubs, the
    /// played results, the rounds done, and the season seed; Restore rebuilds the league and replays
    /// the results so the resumed season continues deterministically — remaining fixtures are seeded from
    /// <see cref="SeasonSaveData.MatchSeed"/>, so they reproduce an uninterrupted run exactly.
    /// </summary>
    public sealed class SeasonSaveMapper
    {
        public SeasonSaveData Capture(League league, LeagueSeason season, ulong matchSeed)
        {
            var data = new SeasonSaveData
            {
                SchemaVersion = SaveSchema.CurrentVersion,
                LeagueName = league.Name,
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
            var clubs = new List<Club>(data.Clubs.Count);
            foreach (ClubSaveData club in data.Clubs)
            {
                clubs.Add(new Club(new ClubId(club.Id), club.Name, new TeamStrength(club.Attack, club.Midfield, club.Defence)));
            }

            var league = new League(data.LeagueName, clubs);

            var results = new List<MatchResult>(data.Results.Count);
            foreach (MatchResultSaveData result in data.Results)
            {
                // Per-goal events are not persisted (score is enough to rebuild the table); the
                // narrative layer (Faz 5) will decide what match detail a save must keep.
                results.Add(new MatchResult(new ClubId(result.Home), new ClubId(result.Away), result.HomeGoals, result.AwayGoals, Array.Empty<MatchEvent>()));
            }

            LeagueSeason season = LeagueSeason.Restore(league, data.PlayedRounds, results);
            return new RestoredSeason(league, season);
        }
    }
}
