using System;
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
    /// role, so a club's academy keeps feeding it. Retirement is deterministic and believable — no one plays
    /// past a hard age (later for keepers), and in the twilight years a fading, lower-rated player is likelier
    /// to hang up his boots than a star who plays on. Youth are drawn from a band around the club's current
    /// level, so a strong club's academy stays strong — tier persists. New players get fresh ids past every
    /// existing one. The guaranteed academy gem (the ongoing discovery fantasy) seeds into that intake, so it
    /// can arrive even in a season with no retirements.
    /// <para>
    /// <b>Squad size is unbounded here, deliberately.</b> Retirement replaces one-for-one, so it never opens
    /// room; the intake is pure growth and nothing in this class stops it. A roster therefore gains
    /// <see cref="RenewalSettings.YouthIntakePerSeason"/> players every season, for ever. Measured on the
    /// shipped defaults: 20 at generation, then 30 after ten rollovers, 40 after twenty, 70 after fifty.
    /// That is not a defect: the old <c>MaxSquadSize</c> gate made the
    /// "guaranteed" intake stop dead once a squad reached 25, so from season 6 on no club in the league ever
    /// received another academy player. The owner chose growth over that silence (2026-08-09, PROGRESS #30).
    /// The intended drain is <b>expiring contracts</b> — players whose deal runs out leave the club of their
    /// own accord, FM-style — which is not built yet. Until it lands, unbounded growth is the known,
    /// accepted cost. Do not "fix" it by putting a cap back; build contracts.
    /// </para>
    /// </summary>
    public sealed class SquadRenewal
    {
        private readonly PlayerGenerator _generator;
        private readonly RenewalSettings _settings;

        // One generator reseeded at each decision point (retirement roll, intake-role tie-break,
        // per-youth generation) instead of a fresh instance per decision (PERFORMANCE §8). The
        // decisions are sequential, and reseeding reproduces the exact per-seed streams, so
        // determinism is unchanged.
        private readonly SplitMix64RandomNumberGenerator _rng = new SplitMix64RandomNumberGenerator(0);

        // The thinnest-role search runs once per academy youth, per club, per season, so its working sets
        // are fields cleared and refilled per call rather than a fresh Dictionary and List each time
        // (PERFORMANCE §4). Both are indexed by (int)PlayerRole — the role set is a fixed, dense
        // 0..11 enum — so there is no hashing and no enum comparer, which also sidesteps the IL2CPP
        // enum-key boxing hazard. AllRoles must stay in step with the enum for the indexing to hold.
        private readonly int[] _roleCounts = new int[AllRoles.Length];
        private readonly PlayerRole[] _thinnestCandidates = new PlayerRole[AllRoles.Length];

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
        /// intake on top — always, whatever the squad already holds, so the roster grows by
        /// <see cref="RenewalSettings.YouthIntakePerSeason"/> a season without bound (see the class summary)
        /// — advancing <paramref name="nextPlayerId"/> past the ids it hands out.
        /// When <paramref name="seedGem"/> is set and any youth joins, the first
        /// intake is drawn from the gem context (low visible ability, high hidden potential) — the season's
        /// guaranteed academy wonderkid, indistinguishable from an ordinary prospect on ability alone, so only
        /// scouting or playing him reveals what he is. The caller schedules the gem rarely (a per-club cadence),
        /// never as a per-player chance. Deterministic in the season seed, the season number, and the player ids.
        /// </summary>
        public Squad Renew(Squad squad, ulong seasonSeed, int seasonNumber, ref int nextPlayerId, bool seedGem = false)
        {
            IReadOnlyList<Player> squadPlayers = squad.Players;
            var kept = new List<Player>(squadPlayers.Count);
            var intakeRoles = new List<PlayerRole>();

            for (int i = 0; i < squadPlayers.Count; i++)
            {
                Player player = squadPlayers[i];
                _rng.Reseed(RetireSeed(seasonSeed, player.Id.Value, seasonNumber));
                if (Retirement.Retires(player, _settings, _rng))
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
            // thinnest role, so even a season with no retirements brings talent through. Unconditional — no
            // squad-size gate. There used to be one (`projected < MaxSquadSize`) and it made the guarantee a
            // lie: retirees are replaced one-for-one, so nothing ever freed a slot, and every club went silent
            // for good on reaching 25. The roster now grows every season instead; expiring contracts are the
            // intended drain and are not built yet (class summary, PROGRESS #30).
            _rng.Reseed(IntakeRoleSeed(seasonSeed, seasonNumber));
            for (int i = 0; i < _settings.YouthIntakePerSeason; i++)
            {
                intakeRoles.Add(ThinnestRole(kept, intakeRoles, _rng));
            }

            if (intakeRoles.Count > 0)
            {
                GenerationContext youth = YouthContext(squad);
                for (int i = 0; i < intakeRoles.Count; i++)
                {
                    int id = nextPlayerId++;
                    _rng.Reseed(IntakeSeed(seasonSeed, id, seasonNumber));
                    GenerationContext context = seedGem && i == 0 ? GemContext() : youth;
                    kept.Add(_generator.Generate(new PlayerId(id), context, intakeRoles[i], _rng));
                }
            }

            // `kept` was built here and is handed over — nothing else holds it — so the squad takes it
            // rather than copying a roster-sized list per club per season (PERFORMANCE §8).
            return Squad.Owning(kept);
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
        private PlayerRole ThinnestRole(List<Player> kept, List<PlayerRole> planned, IRandom rng)
        {
            Array.Clear(_roleCounts, 0, _roleCounts.Length);

            foreach (Player player in kept)
            {
                _roleCounts[(int)player.Role]++;
            }

            foreach (PlayerRole role in planned)
            {
                _roleCounts[(int)role]++;
            }

            int min = int.MaxValue;
            for (int i = 0; i < AllRoles.Length; i++)
            {
                int count = _roleCounts[(int)AllRoles[i]];
                if (count < min)
                {
                    min = count;
                }
            }

            // Candidates are collected in AllRoles order and picked by index, exactly as before, so the
            // tie-break draws the same role from the same rng stream (NON-NEGOTIABLE #2).
            int candidateCount = 0;
            for (int i = 0; i < AllRoles.Length; i++)
            {
                if (_roleCounts[(int)AllRoles[i]] == min)
                {
                    _thinnestCandidates[candidateCount++] = AllRoles[i];
                }
            }

            return _thinnestCandidates[rng.NextInt(candidateCount)];
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
        // Every number in that band is balance and comes from the settings (NON-NEGOTIABLE #3); it used
        // to be literals here, beside the gem band that was already config.
        private GenerationContext YouthContext(Squad squad)
        {
            int average = AverageRating(squad);
            return new GenerationContext
            {
                MinAge = _settings.YouthMinAge,
                MaxAge = _settings.YouthMaxAge,
                MinAbility = (byte)Clamp(average + _settings.YouthMinAbilityOffset, _settings.YouthMinAbilityFloor, _settings.YouthMinAbilityCeiling),
                MaxAbility = (byte)Clamp(average + _settings.YouthMaxAbilityOffset, _settings.YouthMaxAbilityFloor, _settings.YouthMaxAbilityCeiling),
                MinPotential = (byte)Clamp(average + _settings.YouthMinPotentialOffset, _settings.YouthMinPotentialFloor, _settings.YouthMinPotentialCeiling),
                MaxPotential = (byte)Clamp(average + _settings.YouthMaxPotentialOffset, _settings.YouthMaxPotentialFloor, _settings.YouthMaxPotentialCeiling),
            };
        }

        private int AverageRating(Squad squad)
        {
            IReadOnlyList<Player> players = squad.Players;
            if (players.Count == 0)
            {
                return _settings.EmptySquadAverageRating;
            }

            double total = 0.0;
            for (int i = 0; i < players.Count; i++)
            {
                total += PlayerRatings.ForRole(players[i]);
            }

            return (int)(total / players.Count);
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
