using System.Collections.Generic;
using Gaffer.Application.Generation;
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
    /// (<see cref="PlayerDevelopment"/>), its veterans retire and same-role youth come through
    /// (<see cref="SquadRenewal"/>), and each club's strength is re-derived from the renewed roster. This is
    /// what makes the discover-grow-sell flip real in a run — a scouted teenager only pays off across seasons,
    /// rivals age too, and rosters renew instead of ageing into the ground, so the table's spread shifts year
    /// on year. Deterministic: each player develops and retires through his own rng seeded from the season
    /// seed, his id, and the season number, and new youth get fresh ids past every existing one, so a run
    /// reproduces exactly and one club's changes never perturb another's. Squad-less clubs pass through untouched.
    /// </summary>
    public sealed class SeasonTransition
    {
        private readonly PlayerDevelopment _development;
        private readonly EffectiveStrengthBuilder _strengthBuilder;
        private readonly SquadRenewal _renewal;
        private readonly int _gemCadenceSeasons;

        public SeasonTransition()
            : this(DevelopmentSettings.Default, RenewalSettings.Default)
        {
        }

        public SeasonTransition(DevelopmentSettings developmentSettings)
            : this(developmentSettings, RenewalSettings.Default)
        {
        }

        /// <summary>Rolls seasons on with specific development and renewal balance (from config assets), so
        /// tuning the numbers changes how every squad grows, ages, retires, and brings youth through a run.</summary>
        public SeasonTransition(DevelopmentSettings developmentSettings, RenewalSettings renewalSettings)
            : this(developmentSettings, renewalSettings, null)
        {
        }

        /// <summary>Also takes the trait catalog (from config assets): development resolves each player's
        /// growth/decline traits through it, youth are generated against it, and re-derived strengths read
        /// it. Null falls back to the built-in default.</summary>
        public SeasonTransition(DevelopmentSettings developmentSettings, RenewalSettings renewalSettings, Gaffer.Domain.Traits.TraitCatalog traits)
        {
            Gaffer.Domain.Traits.TraitCatalog catalog = traits ?? Gaffer.Domain.Traits.TraitCatalog.Default;
            _development = new PlayerDevelopment(developmentSettings, catalog);
            _renewal = new SquadRenewal(new PlayerGenerator(catalog), renewalSettings);
            _strengthBuilder = new EffectiveStrengthBuilder(catalog);
            _gemCadenceSeasons = renewalSettings.GemCadenceSeasons;
        }

        /// <summary>
        /// Returns a new league for <paramref name="nextSeasonNumber"/> with every squad aged and developed.
        /// The season seed and the season number keep successive rollovers distinct yet reproducible.
        /// </summary>
        public League ToNextSeason(League league, ulong seasonSeed, int nextSeasonNumber)
        {
            // New youth are handed ids past every player already in the league, so intake never collides with
            // an existing id (across clubs or across earlier seasons' arrivals).
            int nextPlayerId = MaxPlayerId(league) + 1;

            var clubs = new List<Club>(league.Clubs.Count);
            foreach (Club club in league.Clubs)
            {
                if (club.Squad == null)
                {
                    clubs.Add(club);
                    continue;
                }

                Squad developed = DevelopSquad(club.Squad, seasonSeed, nextSeasonNumber);
                bool seedGem = IsGemSeason(club.Id.Value, nextSeasonNumber);
                Squad renewed = _renewal.Renew(developed, seasonSeed, nextSeasonNumber, ref nextPlayerId, seedGem);
                TeamStrength strength = _strengthBuilder.Build(renewed);
                clubs.Add(new Club(club.Id, club.Name, renewed, strength));
            }

            return new League(league.Name, clubs);
        }

        // True when this club is due its academy gem this season, on the settings' rare guaranteed cadence
        // (never a per-player chance). Phase-shifted by the club id so clubs do not all produce in the same
        // year — gems trickle across the league, one club at a time.
        private bool IsGemSeason(int clubId, int seasonNumber)
        {
            return (seasonNumber + clubId) % _gemCadenceSeasons == 0;
        }

        private static int MaxPlayerId(League league)
        {
            int max = -1;
            foreach (Club club in league.Clubs)
            {
                if (club.Squad == null)
                {
                    continue;
                }

                foreach (Player player in club.Squad.Players)
                {
                    if (player.Id.Value > max)
                    {
                        max = player.Id.Value;
                    }
                }
            }

            return max;
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
