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
    /// joins for each one, so a run's rosters stay viable instead of ageing into the ground. On top of that a
    /// guaranteed academy intake joins every season — even one with no retirements — at the squad's thinnest
    /// role, so a club's academy keeps feeding it, and the roster grows toward a cap (<see cref="RenewalSettings.MaxSquadSize"/>)
    /// instead of holding fixed. Retirement is deterministic and believable — no one plays past a hard age
    /// (later for keepers), and in the twilight years a fading, lower-rated player is likelier to hang up his
    /// boots than a star who plays on. Youth are drawn from a band around the club's current level, so a strong
    /// club's academy stays strong — tier persists. New players get fresh ids past every existing one. The
    /// guaranteed academy gem (the ongoing discovery fantasy) seeds into that intake, so it can arrive even in a
    /// season with no retirements.
    /// </summary>
    public sealed class SquadRenewal
    {
        private readonly PlayerGenerator _generator;
        private readonly RenewalSettings _settings;

        public SquadRenewal(PlayerGenerator generator)
            : this(generator, RenewalSettings.Default)
        {
        }

        public SquadRenewal(PlayerGenerator generator, RenewalSettings settings)
        {
            _generator = generator;
            _settings = settings;
        }

        /// <summary>
        /// Retires the squad's veterans, brings in a same-role youth for each, and adds a guaranteed academy
        /// intake on top (up to <see cref="RenewalSettings.MaxSquadSize"/>), advancing <paramref name="nextPlayerId"/>
        /// past the ids it hands out. When <paramref name="seedGem"/> is set and any youth joins, the first
        /// intake is drawn from the gem context (low visible ability, high hidden potential) — the season's
        /// guaranteed academy wonderkid, indistinguishable from an ordinary prospect on ability alone, so only
        /// scouting or playing him reveals what he is. The caller schedules the gem rarely (a per-club cadence),
        /// never as a per-player chance. Deterministic in the season seed, the season number, and the player ids.
        /// </summary>
        public Squad Renew(Squad squad, ulong seasonSeed, int seasonNumber, ref int nextPlayerId, bool seedGem = false)
        {
            var kept = new List<Player>(squad.Players.Count);
            var intakeRoles = new List<PlayerRole>();

            foreach (Player player in squad.Players)
            {
                var rng = new SplitMix64RandomNumberGenerator(RetireSeed(seasonSeed, player.Id.Value, seasonNumber));
                if (Retires(player, rng))
                {
                    // A retiree is replaced by a youth of the same role — the line balance is preserved.
                    intakeRoles.Add(player.Role);
                }
                else
                {
                    kept.Add(player);
                }
            }

            // The guaranteed academy intake, beyond replacing retirees: each new youth fills the squad's
            // thinnest role, so even a season with no retirements brings talent through, and the roster grows
            // toward the cap rather than holding fixed.
            var pickRng = new SplitMix64RandomNumberGenerator(IntakeRoleSeed(seasonSeed, seasonNumber));
            int projected = kept.Count + intakeRoles.Count;
            for (int i = 0; i < _settings.YouthIntakePerSeason && projected < _settings.MaxSquadSize; i++)
            {
                intakeRoles.Add(ThinnestRole(kept, intakeRoles, pickRng));
                projected++;
            }

            if (intakeRoles.Count > 0)
            {
                GenerationContext youth = YouthContext(squad);
                for (int i = 0; i < intakeRoles.Count; i++)
                {
                    int id = nextPlayerId++;
                    var rng = new SplitMix64RandomNumberGenerator(IntakeSeed(seasonSeed, id, seasonNumber));
                    GenerationContext context = seedGem && i == 0 ? GemContext() : youth;
                    kept.Add(_generator.Generate(new PlayerId(id), context, intakeRoles[i], rng));
                }
            }

            return new Squad(kept);
        }

        // Every specific role, so a thinnest-role search sees positions the squad has none of as well.
        private static readonly PlayerRole[] AllRoles =
        {
            PlayerRole.Goalkeeper, PlayerRole.RightBack, PlayerRole.CentreBack, PlayerRole.LeftBack,
            PlayerRole.DefensiveMidfield, PlayerRole.CentralMidfield, PlayerRole.AttackingMidfield,
            PlayerRole.RightMidfield, PlayerRole.LeftMidfield, PlayerRole.RightWing, PlayerRole.LeftWing,
            PlayerRole.Striker,
        };

        // The role the squad is thinnest in (counting the youths already planned this intake), so successive
        // academy arrivals spread across the positions of need. Ties are broken by the deterministic pick rng,
        // so a thin squad does not always fill the same role first and the choice still reproduces.
        private static PlayerRole ThinnestRole(List<Player> kept, List<PlayerRole> planned, IRandom rng)
        {
            var counts = new Dictionary<PlayerRole, int>(AllRoles.Length);
            foreach (PlayerRole role in AllRoles)
            {
                counts[role] = 0;
            }

            foreach (Player player in kept)
            {
                counts[player.Role]++;
            }

            foreach (PlayerRole role in planned)
            {
                counts[role]++;
            }

            int min = int.MaxValue;
            foreach (PlayerRole role in AllRoles)
            {
                if (counts[role] < min)
                {
                    min = counts[role];
                }
            }

            var candidates = new List<PlayerRole>();
            foreach (PlayerRole role in AllRoles)
            {
                if (counts[role] == min)
                {
                    candidates.Add(role);
                }
            }

            return candidates[rng.NextInt(candidates.Count)];
        }

        // Twilight is where retirement starts to bite and Hard is where it is certain — both later for
        // keepers, who play on longest. Between them the odds climb with age and ease for a higher rating,
        // so a star lingers while a fading squad player calls it a day.
        private bool Retires(Player player, IRandom rng)
        {
            bool keeper = player.Role == PlayerRole.Goalkeeper;
            int twilight = keeper ? _settings.KeeperTwilightAge : _settings.OutfielderTwilightAge;
            int hard = keeper ? _settings.KeeperHardAge : _settings.OutfielderHardAge;

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
            double chance = progress * (1.0 - (_settings.RetirementRatingEase * (rating / 100.0)));
            return rng.NextDouble() < chance;
        }

        // The guaranteed academy gem (TDD §5): low visible ability — no higher than an ordinary prospect, so
        // he hides in plain sight — but a high, rare ceiling. Fixed, not tier-scaled: an undervalued gem is
        // the point, whatever the club. Cheap to buy on scouted potential, worth a fortune once he grows.
        private GenerationContext GemContext()
        {
            return new GenerationContext
            {
                MinAge = _settings.YouthMinAge,
                MaxAge = _settings.YouthMaxAge,
                MinAbility = _settings.GemMinAbility,
                MaxAbility = _settings.GemMaxAbility,
                MinPotential = _settings.GemMinPotential,
                MaxPotential = _settings.GemMaxPotential,
            };
        }

        // Youth arrive raw but with a ceiling, drawn from a band around the club's current level — so a
        // strong squad's intake is stronger and its best prospects can climb past today's first team.
        private GenerationContext YouthContext(Squad squad)
        {
            int average = AverageRating(squad);
            return new GenerationContext
            {
                MinAge = _settings.YouthMinAge,
                MaxAge = _settings.YouthMaxAge,
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

        // Seeds the thinnest-role tie-break, once per squad per season (its own offset), independent of the
        // per-youth generation rng.
        private static ulong IntakeRoleSeed(ulong seasonSeed, int seasonNumber)
        {
            return Mix(seasonSeed ^ 0x526F6C655069636BUL, 0, seasonNumber);
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
