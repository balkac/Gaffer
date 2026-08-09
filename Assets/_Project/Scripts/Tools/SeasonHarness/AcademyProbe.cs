using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Season;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Leagues;
using Gaffer.Domain.Players;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>One season's academy figures for the whole league, as observed from outside the renewal.</summary>
    public sealed class AcademySeasonRow
    {
        public AcademySeasonRow(int seasonNumber, int clubs, int intakeGranted, int intakeCancelled, int retirements, double meanSquadSizeBefore)
        {
            SeasonNumber = seasonNumber;
            Clubs = clubs;
            IntakeGranted = intakeGranted;
            IntakeCancelled = intakeCancelled;
            Retirements = retirements;
            MeanSquadSizeBefore = meanSquadSizeBefore;
        }

        public int SeasonNumber { get; }

        public int Clubs { get; }

        /// <summary>Club-seasons in which an academy youth arrived on top of the retirement replacements.</summary>
        public int IntakeGranted { get; }

        /// <summary>Club-seasons in which the guaranteed intake was silently dropped.</summary>
        public int IntakeCancelled { get; }

        public int Retirements { get; }

        public double MeanSquadSizeBefore { get; }
    }

    public sealed class AcademyReport
    {
        public AcademyReport(int youthIntakePerSeason, IReadOnlyList<AcademySeasonRow> seasons, int firstSilentSeason)
        {
            YouthIntakePerSeason = youthIntakePerSeason;
            Seasons = seasons;
            FirstSilentSeason = firstSilentSeason;
        }

        public int YouthIntakePerSeason { get; }

        public IReadOnlyList<AcademySeasonRow> Seasons { get; }

        /// <summary>
        /// The first season number in which no club in the league received an intake, or 0 if that never
        /// happened. Since the squad cap was removed (2026-08-09) this must stay 0 — the intake is
        /// unconditional, so a non-zero value means something new is swallowing it.
        /// </summary>
        public int FirstSilentSeason { get; }
    }

    /// <summary>
    /// Measures what the "guaranteed" academy intake actually delivers over a run, from outside
    /// <see cref="SquadRenewal"/>: it diffs each club's squad across a season rollover by player id, so
    /// leavers and arrivals are counted from the rosters rather than from anything the renewal chose to
    /// report.
    /// <para>
    /// The arithmetic that makes the diff readable: renewal replaces every retiree with a same-role youth,
    /// so arrivals always equal retirements PLUS whatever the academy added. Arrivals above retirements is
    /// therefore exactly the guaranteed intake, and arrivals equal to retirements is exactly the intake
    /// being dropped — which is the thing the owner sees as "no one came through this year".
    /// </para>
    /// <para>
    /// Since the squad cap was removed (2026-08-09) the "cancelled" column should stay at zero for ever and
    /// the mean-squad-size column is the thing to read: it climbs by <c>YouthIntakePerSeason</c> a season,
    /// without bound, until expiring contracts exist to drain it.
    /// </para>
    /// <para>Raw English throughout: never-shipped developer tooling, the NON-NEGOTIABLE #8 exemption.</para>
    /// </summary>
    public sealed class AcademyProbe
    {
        public AcademyReport Measure(int clubCount, int seasons, ulong seed, RenewalSettings renewal)
        {
            RenewalSettings settings = renewal ?? RenewalSettings.Default;
            var generator = new LeagueGenerator(new SquadGenerator(new PlayerGenerator()));
            League league = generator.Generate(clubCount, new SplitMix64RandomNumberGenerator(seed));

            var transition = new SeasonTransition(Gaffer.Application.Progression.DevelopmentSettings.Default, settings);
            var rows = new List<AcademySeasonRow>(seasons);
            int firstSilentSeason = 0;

            for (int season = 1; season <= seasons; season++)
            {
                var before = new List<Squad>(league.Clubs.Count);
                int sizeTotal = 0;
                for (int i = 0; i < league.Clubs.Count; i++)
                {
                    Squad squad = league.Clubs[i].Squad;
                    before.Add(squad);
                    sizeTotal += squad == null ? 0 : squad.Players.Count;
                }

                league = transition.ToNextSeason(league, seed, season);

                int granted = 0;
                int cancelled = 0;
                int retirements = 0;
                for (int i = 0; i < league.Clubs.Count; i++)
                {
                    Squad old = before[i];
                    Squad renewed = league.Clubs[i].Squad;
                    if (old == null || renewed == null)
                    {
                        continue;
                    }

                    int left = CountMissing(old, renewed);
                    int joined = CountMissing(renewed, old);
                    retirements += left;

                    if (joined > left)
                    {
                        granted++;
                    }
                    else
                    {
                        cancelled++;
                    }
                }

                if (granted == 0 && firstSilentSeason == 0)
                {
                    firstSilentSeason = season;
                }

                rows.Add(new AcademySeasonRow(
                    season,
                    granted + cancelled,
                    granted,
                    cancelled,
                    retirements,
                    league.Clubs.Count == 0 ? 0.0 : (double)sizeTotal / league.Clubs.Count));
            }

            return new AcademyReport(settings.YouthIntakePerSeason, rows, firstSilentSeason);
        }

        // How many of `squad`'s players are not in `other`, by id. Rosters start at 20 and now grow a player
        // a season with nothing to cap them, so this pair-scan is quadratic in run length — fine for the
        // dozens-of-seasons runs this probe is used for, and cheaper than a set per club per season
        // (PERFORMANCE §4), but it is not the tool to reach for at thousands of seasons.
        private static int CountMissing(Squad squad, Squad other)
        {
            IReadOnlyList<Player> players = squad.Players;
            int missing = 0;
            for (int i = 0; i < players.Count; i++)
            {
                if (!other.Contains(players[i].Id))
                {
                    missing++;
                }
            }

            return missing;
        }
    }
}
