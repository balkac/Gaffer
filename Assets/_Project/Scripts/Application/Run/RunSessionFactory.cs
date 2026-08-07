using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Serialization;
using Gaffer.Common;
using Gaffer.Domain.Leagues;

namespace Gaffer.Application.Run
{
    /// <summary>
    /// The one place a <see cref="RunSession"/> is built (ARCHITECTURE §6). Two entry points — generate a
    /// world, or resume a saved one — and both wire the same graph from the same
    /// <see cref="RunBalance"/>, so no caller can hand the league generator one trait catalog and the
    /// season another. Framework layers hand their config assets in here and get a run back; they never
    /// construct the pieces themselves, which is precisely what two editor windows had each started doing.
    /// </summary>
    public static class RunSessionFactory
    {
        /// <summary>A league needs at least a home and away round between four clubs to be a league.</summary>
        public const int MinTeams = 4;

        /// <summary>Above this the fixture list stops being a season anyone plays through.</summary>
        public const int MaxTeams = 64;

        /// <summary>
        /// Generates the world and starts season one. <paramref name="setup"/> and
        /// <paramref name="balance"/> may be null for the calibrated defaults; the team count is clamped
        /// into [<see cref="MinTeams"/>, <see cref="MaxTeams"/>] rather than rejected, because a slider
        /// that goes out of range is the caller's UI problem, not a reason to refuse a run.
        /// </summary>
        public static Result<RunSession> Start(RunSetup setup, RunBalance balance)
        {
            RunSetup runSetup = setup ?? RunSetup.Default;
            RunBalance runBalance = Normalize(balance);

            int clubCount = Clamp(runSetup.TeamCount, MinTeams, MaxTeams);

            // The strength builder is passed explicitly: the league is generated through the very catalog
            // the season will re-derive strength through, so generation and play cannot disagree (see
            // LeagueGenerator's own note on why the convenience constructor is not enough here).
            var generator = new LeagueGenerator(
                new SquadGenerator(new PlayerGenerator(runBalance.Traits)),
                runBalance.Traits);

            // A separate rng stream from the match seed, so world generation cannot perturb results.
            League league = generator.Generate(clubCount, new SplitMix64RandomNumberGenerator(runSetup.Seed ^ 0x5EEDD5EEDUL));

            return Result<RunSession>.Success(new RunSession(runSetup, runBalance, league, 1, 0, null));
        }

        /// <summary>
        /// Rebuilds a run from a saved document: the league (with full rosters), the season resumed at the
        /// round it was left on, and the season number. The run continues on the save's own match seed, so
        /// the remaining fixtures reproduce an uninterrupted run exactly even if the caller's setup was
        /// since edited. Finances, the market and drama state are not persisted yet (decision #18), so
        /// they are re-seeded from <paramref name="setup"/> — a reload starts quiet.
        /// </summary>
        public static Result<RunSession> Resume(RunSetup setup, RunBalance balance, SeasonSaveData saved)
        {
            if (saved == null)
            {
                return Result<RunSession>.Failure("There is no saved run to resume.");
            }

            RunSetup runSetup = setup ?? RunSetup.Default;
            RunBalance runBalance = Normalize(balance);

            RestoredSeason restored = new SeasonSaveMapper().Restore(
                saved, runBalance.Traits, runBalance.TacticsBalance, runBalance.Morale);

            if (restored.League.Clubs.Count < MinTeams)
            {
                return Result<RunSession>.Failure(
                    $"The saved run has {restored.League.Clubs.Count} clubs, which is not a league (minimum {MinTeams}).");
            }

            // The mapper builds a season for its own return type; the run needs one wired with its
            // simulator, so it is rebuilt here from the same replayed history rather than by reaching
            // into the mapper (which belongs to the save adapter, not to the run).
            LeagueSeason mapped = restored.Season;
            return Result<RunSession>.Success(new RunSession(
                runSetup.WithSeed(saved.MatchSeed),
                runBalance,
                restored.League,
                restored.SeasonNumber,
                mapped.CurrentRound,
                mapped.PlayedResults));
        }

        // The fallback chain for balance, applied once at the wiring seam rather than by every
        // collaborator for itself (ARCHITECTURE §7): a caller that leaves a member null — an editor
        // window with no config asset assigned for it — gets that type's calibrated default, and the run
        // is wired from a bundle with no holes in it. A member added to RunBalance belongs here too.
        private static RunBalance Normalize(RunBalance balance)
        {
            if (balance == null)
            {
                return RunBalance.Default;
            }

            return new RunBalance
            {
                Simulation = balance.Simulation,
                TacticsBalance = balance.TacticsBalance ?? Gaffer.Application.Simulation.TacticsSettings.Default,
                Scorer = balance.Scorer ?? Gaffer.Application.Simulation.ScorerWeights.Default,
                Development = balance.Development ?? Gaffer.Application.Progression.DevelopmentSettings.Default,
                Renewal = balance.Renewal ?? RenewalSettings.Default,
                Drama = balance.Drama ?? Gaffer.Application.Drama.DramaSettings.Default,
                Morale = balance.Morale ?? Gaffer.Application.Drama.MoraleSettings.Default,
                Economy = balance.Economy ?? Gaffer.Application.Transfers.EconomySettings.Default,
                Scouting = balance.Scouting ?? Gaffer.Application.Transfers.ScoutingSettings.Default,
                Traits = balance.Traits ?? Gaffer.Domain.Traits.TraitCatalog.Default,
                DramaEvents = balance.DramaEvents ?? Gaffer.Domain.Drama.DramaCatalog.Default,
            };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
