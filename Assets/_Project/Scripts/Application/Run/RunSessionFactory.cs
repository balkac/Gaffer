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

            return Result<RunSession>.Success(new RunSession(runSetup, runBalance, league, 1, 0, null, null));
        }

        /// <summary>
        /// Rebuilds a run from a saved document: the league with its rosters, the season resumed at the
        /// round it was left on, and — from schema v6 — the rest of the run the save now carries (money,
        /// tactics, the chosen eleven, the market, morale, the drama engine's memory and any unanswered
        /// event). Anything the document does not carry falls back to <paramref name="setup"/>, which is
        /// what a pre-v6 save does for all of it.
        ///
        /// <para><b>The continuation seed is the caller's to choose, and that is the whole design.</b>
        /// Every match's rng comes from a mix of this seed with the fixture's identity, so the seed decides
        /// the unplayed future. The core cannot invent one — <c>Application</c> may not read a clock, a
        /// GUID, or any other ambient entropy (NON-NEGOTIABLE #2) — so it does not try: the game passes a
        /// session-fresh number and gets football's uncertainty (the same tactics and the same eleven can
        /// still lose), while a test passes a fixed one and gets an exactly reproducible run. Passing
        /// <c>saved.MatchSeed</c> reproduces the save's own future, match for match, which is what
        /// <c>RunSessionTests.Resume_FromACapturedRun_ContinuesTheSameFixturesExactly</c> pins.</para>
        ///
        /// <para><b>Consequence, accepted deliberately: this makes save-scumming possible.</b> Reload a
        /// week you did not like on a fresh seed and the scoreline changes — before v6 it could not, because
        /// the season seed came back out of the file. The owner chose that trade for the uncertainty it
        /// buys; there is no anti-scum machinery here and none is wanted. What a reload does NOT hand back
        /// is the drama budget, the cooldowns or the once-per-run marks: those are saved (v6), so the
        /// scummed week is the same week, played again.</para>
        ///
        /// <para>Already-played results are history and cannot move — they are replayed into the table from
        /// the document, not re-simulated. Only fixtures that have not been played diverge.</para>
        /// </summary>
        public static Result<RunSession> Resume(RunSetup setup, RunBalance balance, SeasonSaveData saved, ulong continuationSeed)
        {
            if (saved == null)
            {
                return Result<RunSession>.Failure("There is no saved run to resume.");
            }

            RunBalance runBalance = Normalize(balance);

            RestoredSeason restored = new SeasonSaveMapper().Restore(saved);

            if (restored.League.Clubs.Count < MinTeams)
            {
                return Result<RunSession>.Failure(
                    $"The saved run has {restored.League.Clubs.Count} clubs, which is not a league (minimum {MinTeams}).");
            }

            // The mapper hands back run state, not a season: the season is built inside RunSession, wired
            // with the run's simulator and catalogs, from this same replayed history. The save adapter owns
            // no simulator (ARCHITECTURE §6), so it is not the mapper's job to produce a playable season.
            return Result<RunSession>.Success(new RunSession(
                ResumedSetup(setup ?? RunSetup.Default, restored.Run, continuationSeed),
                runBalance,
                restored.League,
                restored.SeasonNumber,
                restored.PlayedRounds,
                restored.PlayedResults,
                restored.Run));
        }

        /// <summary>
        /// The setup a resumed run actually plays on: the save's own, on the caller's continuation seed,
        /// with <paramref name="setup"/> filling only what the document does not know.
        /// <para>
        /// The overriding is the point of the change. These knobs — which club is managed, the board's
        /// targets, how big the market is — are facts about THIS run, and re-reading them from a window's
        /// input fields on every load is how a saved run came back managing a different club to a different
        /// target. What still comes from <paramref name="setup"/> is what is not run state: the match
        /// context (the stakes every fixture is played under, a balance setting), and the money and shape
        /// for the one case the document cannot answer — a pre-v6 save.
        /// </para>
        /// <para>Formation and tactics are deliberately NOT filled in here even though
        /// <c>RunSetup</c> carries them: <c>RunSession</c> reads the restored ones directly and falls back
        /// to the setup's itself, so there is one fallback rather than two that could disagree.</para>
        /// </summary>
        private static RunSetup ResumedSetup(RunSetup setup, RunState restored, ulong continuationSeed)
        {
            RunSetupState saved = restored?.Setup;
            if (saved == null)
            {
                return setup.WithSeed(continuationSeed);
            }

            return new RunSetup(
                teamCount: saved.TeamCount,
                seed: continuationSeed,
                managedClubIndex: saved.ManagedClubIndex,
                promotionPosition: saved.PromotionPosition,
                survivalPosition: saved.SurvivalPosition,
                startingCash: setup.StartingCash,
                weeklyWageBudget: setup.WeeklyWageBudget,
                marketSize: saved.MarketSize,
                guaranteedGems: saved.GuaranteedGems,
                formation: setup.Formation,
                tactics: setup.Tactics,
                matchContext: setup.MatchContext);
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

            return new RunBalance(
                simulation: balance.Simulation,
                tacticsBalance: balance.TacticsBalance ?? Gaffer.Application.Simulation.TacticsSettings.Default,
                positionalFit: balance.PositionalFit ?? Gaffer.Application.Simulation.PositionalFitSettings.Default,
                scorer: balance.Scorer ?? Gaffer.Application.Simulation.ScorerWeights.Default,
                development: balance.Development ?? Gaffer.Application.Progression.DevelopmentSettings.Default,
                renewal: balance.Renewal ?? RenewalSettings.Default,
                drama: balance.Drama ?? Gaffer.Application.Drama.DramaSettings.Default,
                morale: balance.Morale ?? Gaffer.Application.Drama.MoraleSettings.Default,
                // Named here because they were NOT, and a bundle that authored either had it dropped on the
                // way in: this rebuild is exhaustive by construction, so anything it forgets to carry is
                // silently replaced by a default. Rivalry behaviour and what counts as an occasion were
                // being reset to calibrated defaults for every caller that set them.
                matchContexts: balance.MatchContexts ?? Gaffer.Application.Season.MatchContextSettings.Default,
                rivals: balance.Rivals ?? Gaffer.Application.Rivals.RivalSettings.Default,
                economy: balance.Economy ?? Gaffer.Application.Transfers.EconomySettings.Default,
                scouting: balance.Scouting ?? Gaffer.Application.Transfers.ScoutingSettings.Default,
                traits: balance.Traits ?? Gaffer.Domain.Traits.TraitCatalog.Default,
                dramaEvents: balance.DramaEvents ?? Gaffer.Domain.Drama.DramaCatalog.Default);
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
