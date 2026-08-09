using System;
using System.Collections.Generic;
using Gaffer.Application.Run;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Tools.SeasonHarness
{
    /// <summary>What one signing in the greedy walk cost and what it left behind.</summary>
    public sealed class MarketSigning
    {
        public MarketSigning(int order, string name, int age, long fee, long weeklyWage, long cashAfter, long wageRoomAfter)
        {
            Order = order;
            Name = name;
            Age = age;
            Fee = fee;
            WeeklyWage = weeklyWage;
            CashAfter = cashAfter;
            WageRoomAfter = wageRoomAfter;
        }

        public int Order { get; }

        public string Name { get; }

        public int Age { get; }

        public long Fee { get; }

        public long WeeklyWage { get; }

        public long CashAfter { get; }

        public long WageRoomAfter { get; }
    }

    public sealed class MarketReport
    {
        public MarketReport(
            string managedClub,
            long startingCash,
            long wageBudget,
            long openingWageBill,
            int squadSize,
            int marketSize,
            int cashAffordable,
            int wageAffordable,
            int bothAffordable,
            long medianFee,
            long medianWage,
            IReadOnlyList<MarketSigning> signings,
            string blockedBy,
            long seasonWageDrain,
            int weeksToInsolvency,
            long inheritedSeasonWageDrain,
            long discretionaryCash,
            int discretionaryAffordable)
        {
            InheritedSeasonWageDrain = inheritedSeasonWageDrain;
            DiscretionaryCash = discretionaryCash;
            DiscretionaryAffordable = discretionaryAffordable;
            ManagedClub = managedClub;
            StartingCash = startingCash;
            WageBudget = wageBudget;
            OpeningWageBill = openingWageBill;
            SquadSize = squadSize;
            MarketSize = marketSize;
            CashAffordable = cashAffordable;
            WageAffordable = wageAffordable;
            BothAffordable = bothAffordable;
            MedianFee = medianFee;
            MedianWage = medianWage;
            Signings = signings;
            BlockedBy = blockedBy;
            SeasonWageDrain = seasonWageDrain;
            WeeksToInsolvency = weeksToInsolvency;
        }

        public string ManagedClub { get; }

        public long StartingCash { get; }

        public long WageBudget { get; }

        /// <summary>What the inherited squad already costs each week, before a single signing.</summary>
        public long OpeningWageBill { get; }

        public int SquadSize { get; }

        public int MarketSize { get; }

        /// <summary>Prospects whose fee fits the opening transfer cash, ignoring wages.</summary>
        public int CashAffordable { get; }

        /// <summary>Prospects whose wage fits the opening wage headroom, ignoring the fee.</summary>
        public int WageAffordable { get; }

        /// <summary>Prospects that clear both — the only ones a manager can actually click.</summary>
        public int BothAffordable { get; }

        public long MedianFee { get; }

        public long MedianWage { get; }

        /// <summary>The signings the greedy walk got through before both budgets stopped it.</summary>
        public IReadOnlyList<MarketSigning> Signings { get; }

        /// <summary>Which budget ran out first: "cash", "wage room", "both at once", or "nothing left to buy".</summary>
        public string BlockedBy { get; }

        /// <summary>What the post-signing wage bill drains from transfer cash over a whole season.</summary>
        public long SeasonWageDrain { get; }

        /// <summary>Match weeks the post-signing cash survives that drain, or 0 if it lasts the season.</summary>
        public int WeeksToInsolvency { get; }

        /// <summary>
        /// What a season of the INHERITED wage bill costs — before a single signing. The transfer cash is
        /// also the current account the weekly wages are paid from
        /// (<see cref="Finances.PayWeeklyWages"/>) and there is no income yet, so this comes off the top
        /// of the advertised transfer budget whether the manager signs anyone or not.
        /// </summary>
        public long InheritedSeasonWageDrain { get; }

        /// <summary>The cash actually free to spend on fees: what is left once the season's inherited wages are covered.</summary>
        public long DiscretionaryCash { get; }

        /// <summary>Prospects the market can sell for that, rather than for the headline cash figure.</summary>
        public int DiscretionaryAffordable { get; }
    }

    /// <summary>
    /// Measures whether the two-budget model's ratio is tuned: how much of a generated market each budget
    /// can reach on its own, how many signings a manager gets through before one of them stops him, WHICH
    /// one stops him, and what the resulting wage bill then drains out of the transfer cash over a season.
    /// <para>
    /// It shops through <see cref="RunSession"/> rather than calling <see cref="TransferService"/> directly,
    /// so the walk is bound by everything a real manager is bound by — the open window, the live squad, the
    /// run's own economy balance — and the number it reports is the number he would hit.
    /// </para>
    /// <para>
    /// The walk is deliberately greedy-cheapest rather than greedy-best: it maximises the COUNT of
    /// signings, which is the upper bound on "how many can he make", and an upper bound is the honest
    /// shape for a budget measurement. Buying the best players first would answer a different question.
    /// </para>
    /// <para>Raw English throughout: never-shipped developer tooling, the NON-NEGOTIABLE #8 exemption.</para>
    /// </summary>
    public sealed class MarketProbe
    {
        public Result<MarketReport> Measure(RunSetup setup)
        {
            Result<RunSession> started = RunSessionFactory.Start(setup ?? RunSetup.Default, null);
            if (started.IsFailure)
            {
                return Result<MarketReport>.Failure(started.Error);
            }

            RunSession session = started.Value;
            Finances opening = session.Finances;
            int openingSquadSize = session.Squad == null ? 0 : session.Squad.Players.Count;

            // Wages are paid out of the transfer cash every match week and nothing pays in yet, so the
            // budget a manager can really spend on fees is the headline figure minus a season of the wage
            // bill he inherited. Measured beside the headline because the gap between them is the answer
            // to "is the ratio tuned".
            long inheritedDrain = opening.WeeklyWageBill * session.RoundCount;
            long discretionary = opening.Cash - inheritedDrain;

            int cashAffordable = 0;
            int wageAffordable = 0;
            int bothAffordable = 0;
            int discretionaryAffordable = 0;
            var fees = new List<long>(session.Market.Count);
            var wages = new List<long>(session.Market.Count);
            foreach (Player prospect in session.Market)
            {
                long fee = session.FeeOf(prospect);
                long wage = session.WeeklyWageOf(prospect);
                fees.Add(fee);
                wages.Add(wage);

                bool cashFits = fee <= opening.Cash;
                bool wageFits = wage <= opening.WageHeadroom;
                if (cashFits)
                {
                    cashAffordable++;
                }

                if (wageFits)
                {
                    wageAffordable++;
                }

                if (cashFits && wageFits)
                {
                    bothAffordable++;
                }

                if (fee <= discretionary && wageFits)
                {
                    discretionaryAffordable++;
                }
            }

            var signings = new List<MarketSigning>();
            string blockedBy = Shop(session, signings);

            Finances after = session.Finances;
            long drain = after.WeeklyWageBill * session.RoundCount;
            int weeksToInsolvency = 0;
            if (after.WeeklyWageBill > 0 && drain > after.Cash)
            {
                weeksToInsolvency = (int)(after.Cash / after.WeeklyWageBill) + 1;
            }

            return Result<MarketReport>.Success(new MarketReport(
                session.ManagedClubName,
                opening.Cash,
                opening.WeeklyWageBudget,
                opening.WeeklyWageBill,
                openingSquadSize,
                fees.Count,
                cashAffordable,
                wageAffordable,
                bothAffordable,
                Median(fees),
                Median(wages),
                signings,
                blockedBy,
                drain,
                weeksToInsolvency,
                inheritedDrain,
                discretionary,
                discretionaryAffordable));
        }

        // Signs the cheapest prospect that clears both budgets, over and over, until none does — then
        // names whichever budget was the binding one on the prospects that were left.
        private static string Shop(RunSession session, List<MarketSigning> signings)
        {
            while (true)
            {
                Player cheapest = null;
                long cheapestFee = 0L;
                bool anyCashShort = false;
                bool anyWageShort = false;

                foreach (Player prospect in session.Market)
                {
                    long fee = session.FeeOf(prospect);
                    long wage = session.WeeklyWageOf(prospect);
                    bool cashShort = fee > session.Finances.Cash;
                    bool wageShort = wage > session.Finances.WageHeadroom;

                    if (cashShort || wageShort)
                    {
                        anyCashShort |= cashShort;
                        anyWageShort |= wageShort;
                        continue;
                    }

                    if (cheapest == null || fee < cheapestFee)
                    {
                        cheapest = prospect;
                        cheapestFee = fee;
                    }
                }

                if (cheapest == null)
                {
                    if (session.Market.Count == 0)
                    {
                        return "nothing left to buy";
                    }

                    if (anyCashShort && anyWageShort)
                    {
                        return "both at once";
                    }

                    return anyCashShort ? "cash" : "wage room";
                }

                Result<TransferOutcome> signed = session.SignPlayer(cheapest);
                if (signed.IsFailure)
                {
                    // The session refused what the two comparisons said was affordable — a real divergence
                    // between the preview and the rule, and worth surfacing rather than looping forever.
                    return signed.Error;
                }

                signings.Add(new MarketSigning(
                    signings.Count + 1,
                    signed.Value.Player.Name,
                    signed.Value.Player.Age,
                    signed.Value.Fee,
                    signed.Value.WeeklyWage,
                    session.Finances.Cash,
                    session.Finances.WageHeadroom));
            }
        }

        private static long Median(List<long> values)
        {
            if (values.Count == 0)
            {
                return 0L;
            }

            var sorted = new List<long>(values);
            sorted.Sort();
            return sorted[sorted.Count / 2];
        }
    }
}
