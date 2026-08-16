using System.Collections.Generic;
using Gaffer.Application.Simulation;
using Gaffer.Application.Transfers;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Rivals
{
    /// <summary>
    /// The manager of a club you are not managing: rule-based, deterministic, and deliberately worse at
    /// this than you are.
    ///
    /// <para><b>What it is for.</b> Until now the world stood still around the manager — rivals never
    /// signed anybody, so the market only ever shrank when HE bought, and he had first pick of every gem
    /// for ever. The feeling a management game runs on is competition for players: the boy you were
    /// watching all summer goes somewhere else. That is the whole point of this class; the tactical half
    /// is a smaller thing beside it.</para>
    ///
    /// <para><b>Deliberately imperfect, in two ways that are settings rather than accidents.</b> A club
    /// looks at a SAMPLE of the market (<see cref="RivalSettings.MarketSampleSize"/>), so it misses
    /// things — the manager's edge is that he looked harder, and a rival with perfect sight would take
    /// that away. And it signs very few players a summer
    /// (<see cref="RivalSettings.MaxSigningsPerSeason"/>): rivals are meant to take players off the
    /// board, not clear it, because a market found already emptied is not competition, it is a locked
    /// door.</para>
    ///
    /// <para>Pure and deterministic: every decision comes from the club, the market and an injected rng
    /// seeded per club per season, so a run reproduces its rivals' transfer window exactly
    /// (NON-NEGOTIABLE #2).</para>
    /// </summary>
    public sealed class RivalManager
    {
        private readonly RivalSettings _settings;
        private readonly EconomySettings _economy;



        public RivalManager(RivalSettings settings, EconomySettings economy)
        {
            _settings = settings ?? RivalSettings.Default;
            _economy = economy ?? EconomySettings.Default;
        }

        /// <summary>
        /// What this club can spend, derived from how good it is. A derived number, not a ledger — see
        /// <see cref="RivalSettings.TransferBudgetPerStrengthPoint"/> for why the real economy is not
        /// being invented here.
        /// </summary>
        public long BudgetOf(TeamStrength strength)
        {
            double mean = (strength.Attack + strength.Midfield + strength.Defence) / 3.0;
            double above = mean - _settings.BudgetStrengthFloor;
            return above <= 0.0 ? 0L : (long)(above * _settings.TransferBudgetPerStrengthPoint);
        }

        /// <summary>
        /// Runs one club's summer against the shared <paramref name="shortlist"/> — the market's best by
        /// visible ability — taking each player it signs off it, so the next club and the manager cannot
        /// have him.
        ///
        /// <para>Returns the players signed, in the order they were taken. Emptying the shortlist is the
        /// point rather than a side effect: a rival signing that left the player on the board would be a
        /// rival who changed nothing.</para>
        /// </summary>
        public List<Player> Shop(Squad squad, TeamStrength strength, List<Player> shortlist, IRandom rng)
        {
            var signed = new List<Player>();
            if (squad == null || shortlist == null || shortlist.Count == 0 || rng == null)
            {
                return signed;
            }

            long budget = BudgetOf(strength);
            for (int i = 0; i < _settings.MaxSigningsPerSeason && budget > 0L; i++)
            {
                int best = BestSigningIn(squad, shortlist, budget);
                if (best < 0)
                {
                    break;
                }

                Player taken = shortlist[best];
                shortlist.RemoveAt(best);
                signed.Add(taken);
                budget -= TransferService.Fee(taken, _economy);
                squad = squad.Add(taken);
            }

            return signed;
        }

        /// <summary>
        /// The market's best players by CURRENT ability, most able first — what every rival shops from.
        /// Built once per window and shared, so the cost is one pass over the market rather than one per
        /// club. Potential is deliberately not read: see <see cref="RivalSettings.ShortlistSize"/>.
        /// </summary>
        public List<Player> BuildShortlist(IReadOnlyList<Player> market)
        {
            var shortlist = new List<Player>(market != null ? market.Count : 0);
            if (market == null)
            {
                return shortlist;
            }

            for (int i = 0; i < market.Count; i++)
            {
                shortlist.Add(market[i]);
            }

            shortlist.Sort(ByAbilityDescending);
            if (shortlist.Count > _settings.ShortlistSize)
            {
                shortlist.RemoveRange(_settings.ShortlistSize, shortlist.Count - _settings.ShortlistSize);
            }

            return shortlist;
        }

        // A cached comparison delegate and List.Sort — the allocation-free overload (PERFORMANCE §8).
        private static readonly System.Comparison<Player> ByAbilityDescending =
            (left, right) => PlayerRatings.ForRole(right).CompareTo(PlayerRatings.ForRole(left));

        /// <summary>
        /// The tactics a club of this strength sets, against the league it is in. A side that is better
        /// than the division plays on the front foot; one that is worse sits in and counters — which is
        /// what a weaker side actually does, and what makes an upset read as an upset rather than as the
        /// simulation being generous.
        /// </summary>
        public Tactics TacticsFor(TeamStrength strength, double leagueMeanStrength)
        {
            double mean = (strength.Attack + strength.Midfield + strength.Defence) / 3.0;
            double edge = mean - leagueMeanStrength;

            if (edge >= 4.0)
            {
                return new Tactics(Mentality.Attacking, Tempo.Standard, Pressing.Press, Approach.Possession);
            }

            if (edge <= -4.0)
            {
                return new Tactics(Mentality.Defensive, Tempo.Patient, Pressing.Contain, Approach.Counter);
            }

            return Tactics.Balanced;
        }

        // The index in the market of the player who most improves this squad, or -1 when nobody in the
        // sample is worth the money. "Improves" is measured against the club's WEAKEST man in that role,
        // because that is who he would replace.
        private int BestSigningIn(Squad squad, List<Player> shortlist, long budget)
        {
            int best = -1;
            double bestImprovement = _settings.MinimumImprovement;

            for (int index = 0; index < shortlist.Count; index++)
            {
                Player candidate = shortlist[index];
                if (TransferService.Fee(candidate, _economy) > budget)
                {
                    continue;
                }

                double improvement = PlayerRatings.ForRole(candidate) - WeakestIn(squad, candidate.Role);
                if (improvement > bestImprovement)
                {
                    bestImprovement = improvement;
                    best = index;
                }
            }

            return best;
        }

        // A club's worst player in a role — what a signing has to beat. A role the club has nobody for
        // returns zero, so the first body in an empty position is always an improvement.
        private static double WeakestIn(Squad squad, PlayerRole role)
        {
            IReadOnlyList<Player> players = squad.Players;
            double weakest = double.MaxValue;
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i].Role != role)
                {
                    continue;
                }

                double rating = PlayerRatings.ForRole(players[i]);
                if (rating < weakest)
                {
                    weakest = rating;
                }
            }

            return weakest == double.MaxValue ? 0.0 : weakest;
        }

    }
}
