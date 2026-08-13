using System;
using System.Collections.Generic;
using Gaffer.Application.Drama;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Drama;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The guard behind the "allocates nothing" comments in the sim core (PERFORMANCE §11: "a
    /// zero-allocation claim needs a guard test in the suite"). Until this existed nothing in the suite
    /// measured allocation at all, which is how a comment asserting the opposite of the truth about
    /// <c>List.Sort</c> survived a year.
    ///
    /// <para><b>The path the guarantee covers, named</b> (§4 — "'zero allocation' with no named path is
    /// marketing"): one <see cref="LeagueSeason.AdvanceWeek"/> on a 20-club squad league, so ten
    /// matches, running per match <c>StrengthOf → EffectiveStrengthBuilder.Build</c> (which reads
    /// <see cref="MoraleLedger"/>), the memoised <see cref="LineupSelector.SelectBest"/>, then
    /// <c>MatchSimulator.Simulate → PoissonChanceGenerator.GenerateChances →
    /// QualityChanceResolver.ResolvesToGoal → WeightedScorerSelector.SelectScorer</c>, and once per week
    /// <see cref="MoraleLedger.TickWeek"/>. It does <b>not</b> cover season rollover, save/load,
    /// transfers, world generation or presentation — none of those is on the weekly hot path.</para>
    ///
    /// <para><b>The ceiling is not zero, and a fake zero would be worse.</b> A week deliberately
    /// <i>retains</i> its results for the rest of the season: per match one <c>MatchOutcome</c>, the
    /// <c>List&lt;MatchEvent&gt;</c> of its goals with that list's backing array, one
    /// <c>MatchResult</c> and one <c>MatchCommand</c>; per week one <c>List&lt;MatchResult&gt;</c> and
    /// its array, one <c>WeekResult</c>, and the amortised growth of the season's result history. That
    /// is the season's output, not garbage. What must be zero is everything <i>around</i> it — the
    /// scratch buffers, the memoised elevens, the settings reads — and
    /// <see cref="Components_OnTheWeeklyHotPath_AllocateNothing"/> pins each of those directly, where a
    /// regression names itself instead of merely nudging a total.</para>
    ///
    /// <para><b>Instrument, and one instrument rejected on evidence</b> (§11 "mind the instrument";
    /// UNITY §9 on harness artefacts). The figures below come from
    /// <see cref="GC.GetAllocatedBytesForCurrentThread"/>, which is exact, monotonic and scoped to the
    /// thread doing the work: it reads 4,076 B/week warm and 4,149 B/week over the rest of the season,
    /// repeatable to the byte across runs. <see cref="GC.GetTotalMemory(bool)"/> was measured too and is
    /// <b>not usable here</b>: inside a shared test host its deltas are contaminated by every other
    /// thread in the process. With <c>false</c> the same 18 weeks read 4.6–7.7 KB/week depending on run
    /// and window; with <c>true</c> at both ends — which should report only what the season retains — it
    /// read 5.4, 6.4 and 14.5 KB/week on three consecutive runs, i.e. up to 3.5x the bytes the measured
    /// thread allocated in total, which is impossible for a genuine retention figure. A ceiling derived
    /// from it would be measuring the harness, so no assertion is made on it. The "again after the run"
    /// half of §11 is served instead by re-measuring the exact counter over the season's remaining weeks
    /// and comparing it against the warm figure, which is both falsifiable and stable. The harness is
    /// itself checked for a false floor: the delegates and their closures are built before each window
    /// opens and every body is warmed twice, which is why the component probes read a true 0 rather than
    /// "a small number".</para>
    /// </summary>
    public sealed class AllocationGuardTests
    {
        private const int ClubCount = 20;

        /// <summary>
        /// Bytes a warm <c>AdvanceWeek</c> may allocate for the ten matches of one round.
        /// <para>Measured on this baseline (net8.0 bridge, league seed 4242, season seed 7):
        /// <b>4,076 B/week</b> over the first 18 warm weeks and <b>4,149 B/week</b> over the remaining
        /// 19 — about 407 B per match, of which ~256 B is the <c>MatchOutcome</c> and its goal list that
        /// the season keeps. The ceiling is 6 KB: ~48% headroom over the measured figure, and still
        /// tight enough that the regression this exists to catch — re-allocating the auto-picked eleven
        /// per club per match, +20 lists a week, ~2.9 KB — breaks it.</para>
        /// </summary>
        private const long MaxBytesPerWeek = 6 * 1024;

        private const int WarmUpWeeks = 1;
        private const int WarmWindowWeeks = 18;

        private static League SquadLeague()
        {
            var squadGen = new SquadGenerator(new PlayerGenerator());
            var builder = new EffectiveStrengthBuilder();
            var genRng = new SplitMix64RandomNumberGenerator(4242UL);
            var clubs = new List<Club>(ClubCount);
            for (int i = 0; i < ClubCount; i++)
            {
                Squad squad = squadGen.Generate(i * SquadGenerator.SquadSize, new GenerationContext(), genRng);
                clubs.Add(new Club(new ClubId(i), "Club " + i, squad, builder.Build(squad)));
            }

            return new League("Squad League", clubs);
        }

        private static LeagueSeason Season()
        {
            return new LeagueSeason(SquadLeague(), null, null, null, new MatchSimulator(
                new PoissonChanceGenerator(MatchSimulationSettings.Default),
                new QualityChanceResolver()));
        }

        private static MatchContext NormalContext()
        {
            return new MatchContext(MatchImportance.Normal, 10000, isTitleDecider: false, isRivalry: false);
        }

        // Plays `weeks` rounds and returns the bytes allocated per round.
        private static long BytesPerWeek(LeagueSeason season, MatchContext context, int weeks)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < weeks; i++)
            {
                season.AdvanceWeek(context, 7UL);
            }

            return (GC.GetAllocatedBytesForCurrentThread() - before) / weeks;
        }

        [Test]
        public void AdvanceWeek_Warm_StaysUnderThePerWeekByteCeiling()
        {
            LeagueSeason season = Season();
            MatchContext context = NormalContext();

            // Warm first: week one fills the auto-eleven memo for all 20 clubs, grows every scratch buffer
            // to its working size and JITs the whole path. Measuring that would be measuring the warm-up —
            // the §11 false positive this test is written to avoid.
            for (int i = 0; i < WarmUpWeeks; i++)
            {
                season.AdvanceWeek(context, 7UL);
            }

            long perWeek = BytesPerWeek(season, context, WarmWindowWeeks);

            Assert.That(perWeek, Is.LessThanOrEqualTo(MaxBytesPerWeek),
                $"A warm AdvanceWeek allocated {perWeek} B (ceiling {MaxBytesPerWeek} B). Something on the weekly " +
                "path stopped reusing its buffer — Components_OnTheWeeklyHotPath_AllocateNothing names which.");
        }

        [Test]
        public void AdvanceWeek_OverTheRestOfTheSeason_CostsNoMoreThanItDidWarm()
        {
            // §11 wants the constraint asserted "both while warm and again after the run", so that a leak
            // which only appears with play time — a cache keyed per week, a history list growing
            // super-linearly, a ledger that never drops an expired entry — fails too. Week 30 must cost
            // what week 2 cost.
            LeagueSeason season = Season();
            MatchContext context = NormalContext();
            for (int i = 0; i < WarmUpWeeks; i++)
            {
                season.AdvanceWeek(context, 7UL);
            }

            long warm = BytesPerWeek(season, context, WarmWindowWeeks);

            int remaining = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            while (!season.IsComplete)
            {
                season.AdvanceWeek(context, 7UL);
                remaining++;
            }

            Assert.That(remaining, Is.GreaterThan(0), "The season was already over — this measured nothing.");
            long late = (GC.GetAllocatedBytesForCurrentThread() - before) / remaining;

            Assert.That(season.PlayedResults.Count, Is.EqualTo(ClubCount * (ClubCount - 1)),
                "The whole season must have been played, or the ceiling was cleared by doing less work.");
            Assert.That(late, Is.LessThanOrEqualTo(MaxBytesPerWeek),
                $"The season's last {remaining} weeks cost {late} B each (ceiling {MaxBytesPerWeek} B).");
            Assert.That(late, Is.LessThanOrEqualTo(warm + (warm / 4)),
                $"Weekly cost drifted from {warm} B early to {late} B late — something grows with play time.");
        }

        // Bytes allocated by `iterations` calls, with the delegate, its closure and two warm-up calls all
        // outside the window — so what remains is the body's own steady-state cost.
        private static long BytesFor(int iterations, Action body)
        {
            body();
            body();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                body();
            }

            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Test]
        public void Components_OnTheWeeklyHotPath_AllocateNothing()
        {
            // The comments that claim it, asserted one at a time. 1000 iterations each, so a single stray
            // 24-byte List header per call shows up as 24,000 B rather than hiding inside a season total.
            const int calls = 1000;
            League league = SquadLeague();
            Squad squad = league.Clubs[0].Squad;
            var rng = new SplitMix64RandomNumberGenerator(5UL);

            var selector = new LineupSelector();
            Assert.That(BytesFor(calls, () => selector.SelectBest(squad, Formation.F442)), Is.Zero,
                "LineupSelector.SelectBest must reuse its scratch and result buffers.");

            var chanceGenerator = new PoissonChanceGenerator(MatchSimulationSettings.Default);
            var command = new MatchCommand(
                league.Clubs[0].Strength, league.Clubs[1].Strength,
                squad.Players, league.Clubs[1].Squad.Players,
                ChanceProfile.Neutral, ChanceProfile.Neutral,
                NormalContext());
            Assert.That(BytesFor(calls, () => chanceGenerator.GenerateChances(command, rng)), Is.Zero,
                "PoissonChanceGenerator.GenerateChances must reuse its chance buffer.");

            var resolver = new QualityChanceResolver();
            Chance chance = chanceGenerator.GenerateChances(command, rng)[0];
            Assert.That(BytesFor(calls, () => resolver.ResolvesToGoal(chance, rng)), Is.Zero,
                "The chance port must not allocate to answer one shot.");

            var scorerSelector = new WeightedScorerSelector();
            Assert.That(BytesFor(calls, () => scorerSelector.SelectScorer(squad.Players, rng)), Is.Zero,
                "The scorer port must serve a scorer from its memoised weight vectors.");

            var strengthBuilder = new EffectiveStrengthBuilder();
            IReadOnlyList<Player> eleven = selector.SelectBest(squad, Formation.F442);
            var ledger = new MoraleLedger();
            MatchContext context = NormalContext();
            Assert.That(BytesFor(calls, () => strengthBuilder.Build(eleven, Tactics.Balanced, context, ledger)), Is.Zero,
                "Deriving a club's strength from its eleven must not allocate.");
        }

        [Test]
        public void MoraleLedger_TickingAndReading_AllocatesNothingLoadedOrEmpty()
        {
            const int calls = 1000;
            Squad squad = SquadLeague().Clubs[0].Squad;

            var empty = new MoraleLedger();
            Assert.That(BytesFor(calls, () => empty.TickWeek()), Is.Zero,
                "An uneventful tick must not allocate — the emptied-players scratch is reused.");

            var loaded = new MoraleLedger();
            foreach (Player player in squad.Players)
            {
                // Long enough that nothing expires inside the window, so this measures ticking a full
                // ledger rather than the ledger draining itself empty.
                loaded.Apply(player.Id, -5.0, 1_000_000);
            }

            Assert.That(BytesFor(calls, () => loaded.TickWeek()), Is.Zero,
                "Ticking a ledger with a live entry per player must not allocate either.");

            PlayerId first = squad.Players[0].Id;
            Assert.That(BytesFor(calls, () => loaded.PointsOf(first)), Is.Zero,
                "Reading morale is on the per-match strength path and must not allocate.");
        }

        [Test]
        public void DramaEngine_QuietWeek_AllocatesNothing()
        {
            // DramaEngine's claim is specifically about the QUIET week — the common case. An empty catalog
            // makes every week quiet, which isolates that claim from the PendingDrama a fired event
            // legitimately allocates. (Against the default catalog the same 1000 ticks cost 280 B in
            // total, all of it fired events, none of it the quiet path.)
            var quiet = new DramaEngine(new DramaCatalog(Array.Empty<DramaEvent>()), DramaSettings.Default);
            var context = new DramaWeekContext(SquadLeague().Clubs[0].Squad.Players, null, 10, 0, false);
            var rng = new SplitMix64RandomNumberGenerator(11UL);

            Assert.That(BytesFor(1000, () => quiet.TickWeek(context, rng)), Is.Zero,
                "A quiet week must not allocate — the candidate scratch is reused across ticks.");
        }
    }
}
