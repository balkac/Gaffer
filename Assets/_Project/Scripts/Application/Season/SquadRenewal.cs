using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Turns a squad over between seasons: ageing veterans retire and a youth prospect of the same role
    /// joins for each one, so a run's rosters stay viable instead of ageing into the ground. Retirement is
    /// deterministic and believable — no one plays past a hard age (later for keepers), and in the twilight
    /// years a fading, lower-rated player is likelier to hang up his boots than a star who plays on. Intake
    /// keeps the squad size and the line balance fixed (one in for one out, same role) and draws each youth
    /// from a band around the club's current level, so a strong club's academy stays strong — tier persists.
    /// New players get fresh ids past every existing one. Guaranteed-gem seeding into the intake (the ongoing
    /// discovery fantasy) is a later refinement; the starting pool already guarantees the first gems.
    /// </summary>
    public sealed class SquadRenewal
    {
        private readonly PlayerGenerator _generator;

        public SquadRenewal(PlayerGenerator generator)
        {
            _generator = generator;
        }

        /// <summary>
        /// Retires the squad's veterans and brings in a same-role youth for each, advancing
        /// <paramref name="nextPlayerId"/> past the ids it hands out. When <paramref name="seedGem"/> is set
        /// and there is at least one vacancy, one of the intake youths is drawn from the gem context (low
        /// visible ability, high hidden potential) — the season's guaranteed academy wonderkid, indistinguishable
        /// from an ordinary prospect on ability alone, so only scouting or playing him reveals what he is.
        /// The caller schedules this rarely (a per-club cadence), never as a per-player chance. Deterministic
        /// in the season seed, the season number, and the player ids.
        /// </summary>
        public Squad Renew(Squad squad, ulong seasonSeed, int seasonNumber, ref int nextPlayerId, bool seedGem = false)
        {
            var kept = new List<Player>(squad.Players.Count);
            var vacatedRoles = new List<PlayerRole>();

            foreach (Player player in squad.Players)
            {
                var rng = new SplitMix64RandomNumberGenerator(RetireSeed(seasonSeed, player.Id.Value, seasonNumber));
                if (Retires(player, rng))
                {
                    vacatedRoles.Add(player.Role);
                }
                else
                {
                    kept.Add(player);
                }
            }

            if (vacatedRoles.Count > 0)
            {
                GenerationContext youth = YouthContext(squad);
                for (int i = 0; i < vacatedRoles.Count; i++)
                {
                    int id = nextPlayerId++;
                    var rng = new SplitMix64RandomNumberGenerator(IntakeSeed(seasonSeed, id, seasonNumber));
                    GenerationContext context = seedGem && i == 0 ? GemContext : youth;
                    kept.Add(_generator.Generate(new PlayerId(id), context, vacatedRoles[i], rng));
                }
            }

            return new Squad(kept);
        }

        // Twilight is where retirement starts to bite and Hard is where it is certain — both later for
        // keepers, who play on longest. Between them the odds climb with age and ease for a higher rating,
        // so a star lingers while a fading squad player calls it a day.
        private static bool Retires(Player player, IRandom rng)
        {
            bool keeper = player.Role == PlayerRole.Goalkeeper;
            int twilight = keeper ? 36 : 33;
            int hard = keeper ? 43 : 40;

            if (player.Age >= hard)
            {
                return true;
            }

            if (player.Age < twilight)
            {
                return false;
            }

            double progress = (double)(player.Age - twilight) / (hard - twilight);
            double rating = PlayerRatings.ForRole(player);
            double chance = progress * (1.0 - (0.4 * (rating / 100.0)));
            return rng.NextDouble() < chance;
        }

        // The guaranteed academy gem (TDD §5): low visible ability — no higher than an ordinary prospect, so
        // he hides in plain sight — but a high, rare ceiling. Fixed, not tier-scaled: an undervalued gem is
        // the point, whatever the club. Cheap to buy on scouted potential, worth a fortune once he grows.
        private static readonly GenerationContext GemContext = new GenerationContext
        {
            MinAge = 16,
            MaxAge = 18,
            MinAbility = 28,
            MaxAbility = 46,
            MinPotential = 86,
            MaxPotential = 96,
        };

        // Youth arrive raw but with a ceiling, drawn from a band around the club's current level — so a
        // strong squad's intake is stronger and its best prospects can climb past today's first team.
        private static GenerationContext YouthContext(Squad squad)
        {
            int average = AverageRating(squad);
            return new GenerationContext
            {
                MinAge = 16,
                MaxAge = 18,
                MinAbility = (byte)Clamp(average - 25, 25, 60),
                MaxAbility = (byte)Clamp(average - 8, 35, 72),
                MinPotential = (byte)Clamp(average - 3, 45, 85),
                MaxPotential = (byte)Clamp(average + 18, 60, 95),
            };
        }

        private static int AverageRating(Squad squad)
        {
            if (squad.Players.Count == 0)
            {
                return 50;
            }

            double total = 0.0;
            foreach (Player player in squad.Players)
            {
                total += PlayerRatings.ForRole(player);
            }

            return (int)(total / squad.Players.Count);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        // Distinct seed streams for the two decisions (offset constants), so a player's retirement roll is
        // independent of his development roll and of the intake rolls, all reproducible from the same inputs.
        private static ulong RetireSeed(ulong seasonSeed, int playerId, int seasonNumber)
        {
            return Mix(seasonSeed ^ 0x52657469726553UL, playerId, seasonNumber);
        }

        private static ulong IntakeSeed(ulong seasonSeed, int playerId, int seasonNumber)
        {
            return Mix(seasonSeed ^ 0x496E74616B6553UL, playerId, seasonNumber);
        }

        private static ulong Mix(ulong seed, int playerId, int seasonNumber)
        {
            unchecked
            {
                ulong z = seed
                    ^ ((ulong)(uint)playerId * 0x9E3779B97F4A7C15UL)
                    ^ ((ulong)(uint)seasonNumber * 0xD1B54A32D192ED03UL);
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }
    }
}
