using Gaffer.Application.Run;
using Gaffer.Application.Season;
using Gaffer.Common;
using Gaffer.Tools.SeasonHarness;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The diagnostic probes that sit beside the season distribution in the Gate A instrument
    /// (<c>--probe ranks|academy|market</c>). Same reason <c>SeasonHarnessTests</c> exists: a measurement
    /// that only a human can produce by running a console is a measurement that silently rots, so each
    /// probe is reachable from <c>dotnet test</c> and each pins the finding it was written to answer.
    /// <para>
    /// Small samples throughout — these are reachability and behaviour locks, not the full-precision runs.
    /// </para>
    /// </summary>
    public sealed class HarnessProbeTests
    {
        /// <summary>
        /// The rule, pinned league-wide: the academy intake is guaranteed against everything — retirements
        /// AND squad size. Every club receives one every season, for ever, and the mean roster therefore
        /// climbs by <c>YouthIntakePerSeason</c> a season with nothing to flatten it.
        /// <para>
        /// This test used to pin the opposite (<c>AcademyProbe_OnceEverySquadIsAtTheCap_NoClubReceivesAnIntakeAgain</c>):
        /// <c>MaxSquadSize</c> silenced the intake from season 6 on, for every club, permanently, because
        /// retirement replaces one-for-one and never reopened a slot. The owner removed the cap on
        /// 2026-08-09 and accepted unbounded squads; <b>expiring contracts are the intended drain and are
        /// not built yet</b>, so a roster that never stops growing is the design, not a leak. The run here
        /// goes past 25 on purpose — that is where the old behaviour would show itself.
        /// </para>
        /// </summary>
        [Test]
        public void AcademyProbe_EverySeasonPastTheOldCap_EveryClubStillReceivesAnIntake()
        {
            const int Clubs = 6;
            const int Seasons = 12;
            AcademyReport report = new AcademyProbe().Measure(clubCount: Clubs, seasons: Seasons, seed: 20260707UL, renewal: RenewalSettings.Default);

            Assert.That(report.Seasons.Count, Is.EqualTo(Seasons));
            Assert.That(report.FirstSilentSeason, Is.Zero,
                "no season goes by without an academy arrival — the intake is unconditional now.");

            double previous = report.Seasons[0].MeanSquadSizeBefore;
            foreach (AcademySeasonRow row in report.Seasons)
            {
                Assert.That(row.IntakeGranted, Is.EqualTo(row.Clubs),
                    $"season {row.SeasonNumber} dropped an intake for {row.Clubs - row.IntakeGranted} club(s).");
                Assert.That(row.IntakeCancelled, Is.Zero);
            }

            // Squad size is monotone and its step is exactly the intake rate — the growth the owner signed up
            // for, measured rather than assumed.
            for (int i = 1; i < report.Seasons.Count; i++)
            {
                Assert.That(report.Seasons[i].MeanSquadSizeBefore, Is.EqualTo(previous + report.YouthIntakePerSeason).Within(1e-9),
                    $"season {report.Seasons[i].SeasonNumber} did not grow by the intake rate.");
                previous = report.Seasons[i].MeanSquadSizeBefore;
            }

            Assert.That(report.Seasons[Seasons - 1].MeanSquadSizeBefore, Is.GreaterThan(25.0),
                "the run carries past the size the removed cap used to hold every squad at.");
            Assert.That(report.Seasons[Seasons - 1].Retirements, Is.GreaterThan(0),
                "veterans still retire; their one-for-one replacements are why retirement never offsets the growth.");
        }

        /// <summary>
        /// The two-budget model degenerates at the shipped defaults: the wage ceiling cannot refuse anyone
        /// the club can pay a fee for, so cash is the only budget that binds. Locked as a measurement, not
        /// as a target — the owner tunes the ratio, and when he does, this test is what tells him the shape
        /// changed.
        /// </summary>
        [Test]
        public void MarketProbe_AtTheDefaultBudgets_CashIsWhatStopsTheManager()
        {
            Result<MarketReport> measured = new MarketProbe().Measure(RunSetup.Default);

            Assert.That(measured.IsSuccess, Is.True, measured.Error);
            MarketReport report = measured.Value;

            Assert.That(report.WageAffordable, Is.EqualTo(report.MarketSize),
                "no prospect in the market is out of reach of the opening wage headroom.");
            Assert.That(report.CashAffordable, Is.LessThan(report.MarketSize),
                "but plenty are out of reach of the transfer cash.");
            Assert.That(report.BlockedBy, Is.EqualTo("cash"));
            Assert.That(report.InheritedSeasonWageDrain, Is.GreaterThan(report.StartingCash / 2),
                "over half the advertised transfer budget is spoken for by the inherited wage bill.");
        }

        /// <summary>
        /// <c>LeagueGenerator</c>'s ranks are monotone in EXPECTATION — the band centre falls with rank —
        /// but any one league shuffles locally, because the spread inside a club's ability band is wider
        /// than the step between neighbouring bands. Both halves matter: the first is what makes a
        /// pre-season favourite meaningful, the second is what keeps it from being a certainty.
        /// </summary>
        [Test]
        public void GeneratorRankProbe_RanksFallInStrengthOnAverage_ButNotInEveryLeague()
        {
            const int Stride = 4;
            GeneratorRankReport report = new GeneratorRankProbe().Measure(leagues: 24, clubCount: 20, seed: 20260707UL);

            Assert.That(report.Rows.Count, Is.EqualTo(20));

            // Compared a stride apart, not rank by rank. One step of the band centre is ~1.4 points and a
            // club's own strength varies by more than that, so at a test-sized sample two NEIGHBOURING
            // ranks can average out of order without anything being wrong. Four steps apart, the curve is
            // what it claims to be — which is the property worth locking.
            for (int rank = Stride; rank < report.Rows.Count; rank++)
            {
                Assert.That(report.Rows[rank].MeanStrength, Is.LessThan(report.Rows[rank - Stride].MeanStrength),
                    $"rank #{rank + 1} averages stronger than rank #{rank + 1 - Stride}.");
            }

            Assert.That(report.MeanInversions, Is.GreaterThan(0.0),
                "individual leagues do shuffle; a generator that never did would make the table a formality.");
            Assert.That(report.RankOneStrongest, Is.LessThan(report.Leagues));
        }
    }
}
