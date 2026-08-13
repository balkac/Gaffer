using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Progression;
using Gaffer.Application.Simulation;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Season
{
    /// <summary>
    /// Rolls a league on to the next season: every club's squad has a birthday, its veterans retire and
    /// same-role youth come through (<see cref="SquadRenewal"/>), and each club's strength is re-derived
    /// from the renewed roster. This is what makes the discover-grow-sell flip real in a run — a scouted
    /// teenager only pays off across seasons, rivals age too, and rosters renew instead of ageing into the
    /// ground, so the table's spread shifts year on year. Deterministic: each player retires through his
    /// own rng seeded from the season seed, his id, and the season number, and new youth get fresh ids past
    /// every existing one, so a run reproduces exactly and one club's changes never perturb another's.
    /// Squad-less clubs pass through untouched.
    ///
    /// <para><b>Development is not here.</b> Ability now moves during the season, in ticks weighted by
    /// playing time (<c>RunSession.DevelopSquads</c>), so the rollover's job has narrowed to the once-a-year
    /// facts: a year of age, retirement, and intake. See <see cref="PlayerDevelopment.DevelopPeriod"/>.</para>
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
        /// Returns a new league for <paramref name="nextSeasonNumber"/> with every squad a year older and
        /// renewed. The season seed and the season number keep successive rollovers distinct yet reproducible.
        /// </summary>
        public League ToNextSeason(League league, ulong seasonSeed, int nextSeasonNumber)
        {
            // New youth are handed ids past every player already in the league, so intake never collides with
            // an existing id (across clubs or across earlier seasons' arrivals).
            int nextPlayerId = MaxPlayerId(league) + 1;

            var clubs = new List<Club>(league.Clubs.Count);
            for (int i = 0; i < league.Clubs.Count; i++)
            {
                Club club = league.Clubs[i];
                if (club.Squad == null)
                {
                    clubs.Add(club);
                    continue;
                }

                Squad aged = AgeSquad(club.Squad);
                bool seedGem = IsGemSeason(club.Id.Value, nextSeasonNumber);
                Squad renewed = _renewal.Renew(aged, seasonSeed, nextSeasonNumber, ref nextPlayerId, seedGem);
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

        // The highest LEAGUE-NATIVE id on any roster — market ids are skipped on purpose. A signed free
        // agent keeps the id the market gave him, which is far above every league id
        // (PlayerIdSpace.MarketBase); counting him would make the next academy intake allocate inside
        // market space, and the market allocates there too, so two players would end up sharing an id and
        // the journey log would merge two careers. Skipping them keeps league ids where they belong and
        // still leaves academy intake past every league id already in play (LeagueIdSpaceTests).
        private static int MaxPlayerId(League league)
        {
            int max = -1;
            for (int i = 0; i < league.Clubs.Count; i++)
            {
                Club club = league.Clubs[i];
                if (club.Squad == null)
                {
                    continue;
                }

                IReadOnlyList<Player> squadPlayers = club.Squad.Players;
                for (int j = 0; j < squadPlayers.Count; j++)
                {
                    PlayerId id = squadPlayers[j].Id;
                    if (!PlayerIdSpace.IsMarket(id) && id.Value > max)
                    {
                        max = id.Value;
                    }
                }
            }

            return max;
        }

        // A birthday for everyone, and nothing else.
        //
        // ABILITY IS NOT TOUCHED HERE ANY MORE. It used to be: this method ran a whole season of
        // development in one step at the rollover, which is why a player's numbers only ever moved in the
        // summer. Development now arrives during the season, in ticks driven by playing time
        // (RunSession.DevelopSquads → PlayerDevelopment.DevelopPeriod), so growing them again here would
        // hand every squad two seasons of progress a year and quietly double the pace of the whole world.
        // What has to happen exactly once a year is the birthday, and that is what is left.
        private Squad AgeSquad(Squad squad)
        {
            IReadOnlyList<Player> squadPlayers = squad.Players;
            var players = new List<Player>(squadPlayers.Count);
            for (int i = 0; i < squadPlayers.Count; i++)
            {
                players.Add(_development.AgeOneYear(squadPlayers[i]));
            }

            // Built here and handed over with no reference kept, so the squad takes ownership instead
            // of copying it — one list per club per season rather than two (PERFORMANCE §8).
            return Squad.Owning(players);
        }

    }
}
