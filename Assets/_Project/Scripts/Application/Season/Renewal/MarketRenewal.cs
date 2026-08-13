using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Common;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Carries the transfer market across a season boundary: every unattached player ages and develops a
    /// year, those at the end of their careers retire out of the pool, and a new generation arrives to
    /// replace them. The market's answer to <see cref="SquadRenewal"/>, and it asks the same
    /// <see cref="Retirement"/> rule, so an unattached veteran and a squad veteran go on the same terms.
    ///
    /// <para><b>Why it exists.</b> The market used to be thrown away and regenerated from scratch every
    /// summer, with a fresh id block. A prospect you had been watching did not merely fail to improve — he
    /// <em>vanished</em>, and so did every player you had sold, because a sale puts him back in the pool.
    /// That made two things impossible at once: a market worth scouting across seasons, and the journey
    /// log Faz 5 is built on, which cannot follow a player it deletes. The pool is now the world's
    /// unattached players, and it persists (PROGRESS 2026-08-13).</para>
    ///
    /// <para><b>Pool size holds steady.</b> Intake replaces exactly what retirement took, so a 50,000-player
    /// world stays a 50,000-player world instead of ratcheting the way club rosters do (the owner's choice,
    /// 2026-08-13: the FM model, retirement and a new generation in balance). The one exception is the
    /// discovery guarantee — see <paramref name="guaranteedGems"/> on <see cref="Renew"/>.</para>
    ///
    /// <para>Deterministic: each player's development and retirement run on his own stream seeded from the
    /// run seed, his id and the season number, so one player's fate never perturbs another's and the same
    /// run reproduces exactly (NON-NEGOTIABLE #2). New arrivals take ids from this season's market block
    /// (<see cref="PlayerIdSpace"/>), so an id vacated by a retiree is never handed to anyone else.</para>
    /// </summary>
    public sealed class MarketRenewal
    {
        private readonly IPlayerGenerator _generator;
        private readonly PlayerDevelopment _development;
        private readonly RenewalSettings _settings;
        private readonly double _playingTime;

        // One rng reseeded per decision rather than one per player (PERFORMANCE §8): at 50,000 players a
        // fresh instance per development roll and per retirement roll would be 100,000 allocations a
        // season. Reseeding reproduces the identical per-seed stream, so determinism is untouched.
        private readonly SplitMix64RandomNumberGenerator _rng = new SplitMix64RandomNumberGenerator(0);

        public MarketRenewal(IPlayerGenerator generator)
            : this(generator, DevelopmentSettings.Default, RenewalSettings.Default, TraitCatalog.Default)
        {
        }

        public MarketRenewal(IPlayerGenerator generator, DevelopmentSettings development, RenewalSettings renewal, TraitCatalog traits)
        {
            _generator = generator;
            DevelopmentSettings settings = development ?? DevelopmentSettings.Default;
            _development = new PlayerDevelopment(settings, traits ?? TraitCatalog.Default);
            _settings = renewal;
            _playingTime = settings.UnattachedPlayingTime;
        }

        /// <summary>
        /// Moves the whole pool on by <paramref name="seasonFraction"/> of a season's development — the
        /// market's share of the in-season tick, so an unattached prospect improves through the year like
        /// everyone else instead of standing still until he is bought.
        ///
        /// <para><b>Called lazily.</b> At the scale this is built for — 50,000 unattached players — a pass
        /// costs about 22 ms and 5 MB, which is fine once in a while and ruinous every match week (838 ms
        /// and 190 MB a season, measured; PROGRESS 2026-08-13). So the run does not tick the market on the
        /// weekly clock: it ticks it when somebody actually looks, in one bulk catch-up covering every week
        /// owed. The cost lands on opening a screen rather than on pressing "next week", and a manager who
        /// never opens the market pays nothing until the summer.</para>
        ///
        /// <para>Players are developed on <see cref="DevelopmentSettings.UnattachedPlayingTime"/>: nobody
        /// here has a first team, so nobody grows at a starter's rate.</para>
        /// </summary>
        public List<Player> Tick(IReadOnlyList<Player> market, double seasonFraction, ulong seed, int seasonNumber, int round)
        {
            int count = market != null ? market.Count : 0;
            var ticked = new List<Player>(count);
            for (int i = 0; i < count; i++)
            {
                Player player = market[i];
                _rng.Reseed(TickSeed(seed, player.Id.Value, seasonNumber, round));
                ticked.Add(_development.DevelopPeriod(player, seasonFraction, _playingTime, _rng));
            }

            return ticked;
        }

        /// <summary>
        /// Returns the market a season on: everyone has a birthday, those the <see cref="Retirement"/> rule
        /// ends are dropped, and the shortfall is filled with new arrivals drawn from
        /// <paramref name="intake"/>, so the pool comes back to <paramref name="targetSize"/>.
        ///
        /// <para><b>Ability is not touched here.</b> A season's development reaches these players through
        /// <see cref="Tick"/> during the season, so growing them again at the rollover would hand the pool
        /// two seasons of progress a year. The caller ticks the market out to the end of the season and
        /// then calls this (<c>RunSession.RenewMarket</c>).</para>
        ///
        /// <para><paramref name="guaranteedGems"/> of those arrivals are drawn from <paramref name="gem"/>
        /// instead — low visible ability, a high hidden ceiling — which is what keeps the discovery
        /// fantasy alive in a market that is no longer rebuilt every summer (TDD §5). It is a floor on the
        /// intake, not a slice of it: in the rare season where retirement took fewer players than the
        /// guarantee, the gems still arrive and the pool sits that few players above target. At the shipped
        /// scale retirement always takes far more than a handful, so the pool holds at
        /// <paramref name="targetSize"/>; the floor exists so a tiny pool cannot silently lose its gems.</para>
        /// </summary>
        public List<Player> Renew(
            IReadOnlyList<Player> market,
            int targetSize,
            int guaranteedGems,
            GenerationContext intake,
            GenerationContext gem,
            ulong seed,
            int seasonNumber)
        {
            int count = market != null ? market.Count : 0;
            var kept = new List<Player>(count);

            for (int i = 0; i < count; i++)
            {
                // Aged FIRST, then judged: the birthday happens and the retirement question is asked of
                // the player he has become, exactly as SeasonTransition does it for a squad. Asking in the
                // other order would retire him on last year's age.
                Player older = _development.AgeOneYear(market[i]);

                _rng.Reseed(RetireSeed(seed, older.Id.Value, seasonNumber));
                if (!Retirement.Retires(older, _settings, _rng))
                {
                    kept.Add(older);
                }
            }

            int gems = guaranteedGems < 0 ? 0 : guaranteedGems;
            int shortfall = (targetSize < 0 ? 0 : targetSize) - kept.Count;
            int arrivals = shortfall > gems ? shortfall : gems;

            // Each season's arrivals sit in their own id block, so an id freed by a retiree is never
            // reissued and two careers can never merge in the journey log (PlayerIdSpace). The block is
            // also the ceiling on a season's intake: past it the next season's ids would be handed out
            // twice, so the intake is bounded rather than allowed to run into the next block.
            int seasonBase = PlayerIdSpace.SeasonBase(seasonNumber);
            if (arrivals > PlayerIdSpace.SeasonStride)
            {
                arrivals = PlayerIdSpace.SeasonStride;
            }

            for (int i = 0; i < arrivals; i++)
            {
                var id = new PlayerId(seasonBase + i);
                _rng.Reseed(IntakeSeed(seed, id.Value, seasonNumber));
                kept.Add(_generator.Generate(id, i < gems ? gem : intake, _rng));
            }

            return kept;
        }

        // Three separate streams (distinct seed offsets), so a player's development roll, his retirement
        // roll and the generation of a newcomer are independent of one another and each reproducible from
        // the same inputs. The offsets differ from SquadRenewal's on purpose: a market player and a squad
        // player with the same id must not share a fate.

        // The tick stream also carries the ROUND, so successive ticks inside one season are independent of
        // each other — without it every tick in a season would replay one player's identical draw and his
        // development would be a staircase of the same step rather than a career.
        private static ulong TickSeed(ulong seed, int playerId, int seasonNumber, int round)
        {
            unchecked
            {
                return Mix(seed ^ 0x4D6B745469636BUL, playerId, seasonNumber) ^ ((ulong)(uint)round * 0xD6E8FEB86659FD93UL);
            }
        }

        private static ulong RetireSeed(ulong seed, int playerId, int seasonNumber)
        {
            return Mix(seed ^ 0x4D6B7452657400UL, playerId, seasonNumber);
        }

        private static ulong IntakeSeed(ulong seed, int playerId, int seasonNumber)
        {
            return Mix(seed ^ 0x4D6B74496E7400UL, playerId, seasonNumber);
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
