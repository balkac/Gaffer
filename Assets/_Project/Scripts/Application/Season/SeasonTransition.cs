using System.Collections.Generic;
using Gaffer.Application.Progression;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Rolls a league on to the next season: every club's squad ages a year and develops
    /// (<see cref="PlayerDevelopment"/>), and each club's strength is re-derived from the grown roster. This
    /// is what makes the discover-grow-sell flip real in a run — a scouted teenager only pays off across
    /// seasons, and rivals age too, so the table's spread shifts year on year. Deterministic: each player
    /// develops through his own rng seeded from the season seed, his id, and the season number, so a run
    /// reproduces exactly and one player's growth never perturbs another's. Squad-less clubs (strength-only)
    /// pass through untouched. Retirement and youth intake are a later slice; for now the same names age on.
    /// </summary>
    public sealed class SeasonTransition
    {
        private readonly PlayerDevelopment _development = new PlayerDevelopment();
        private readonly EffectiveStrengthBuilder _strengthBuilder = new EffectiveStrengthBuilder();

        /// <summary>
        /// Returns a new league for <paramref name="nextSeasonNumber"/> with every squad aged and developed.
        /// The season seed and the season number keep successive rollovers distinct yet reproducible.
        /// </summary>
        public League ToNextSeason(League league, ulong seasonSeed, int nextSeasonNumber)
        {
            var clubs = new List<Club>(league.Clubs.Count);
            foreach (Club club in league.Clubs)
            {
                if (club.Squad == null)
                {
                    clubs.Add(club);
                    continue;
                }

                Squad developed = DevelopSquad(club.Squad, seasonSeed, nextSeasonNumber);
                TeamStrength strength = _strengthBuilder.Build(developed);
                clubs.Add(new Club(club.Id, club.Name, developed, strength));
            }

            return new League(league.Name, clubs);
        }

        private Squad DevelopSquad(Squad squad, ulong seasonSeed, int seasonNumber)
        {
            var players = new List<Player>(squad.Players.Count);
            foreach (Player player in squad.Players)
            {
                var rng = new SplitMix64RandomNumberGenerator(MixSeed(seasonSeed, player.Id.Value, seasonNumber));
                players.Add(_development.Develop(player, rng));
            }

            return new Squad(players);
        }

        // A well-distributed per-player, per-season seed (SplitMix64 finalizer over the combined inputs), so
        // each player's development is independent of the others and stable across runs.
        private static ulong MixSeed(ulong seasonSeed, int playerId, int seasonNumber)
        {
            unchecked
            {
                ulong z = seasonSeed
                    ^ ((ulong)(uint)playerId * 0x9E3779B97F4A7C15UL)
                    ^ ((ulong)(uint)seasonNumber * 0xD1B54A32D192ED03UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
