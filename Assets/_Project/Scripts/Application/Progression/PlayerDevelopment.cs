using System;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Progression
{
    /// <summary>
    /// Ages a player one season and moves his ability — the missing piece of the discover-grow-sell flip
    /// (GDD §4.4). A young player realises his hidden potential over time: the attributes his role is scored
    /// on climb toward the ceiling, fast at 17 and slowing as he peaks, so his role rating (and the market
    /// value that follows it) rises — that gap between what you paid and what he grows into is the reward.
    /// The ceiling (<see cref="Player.HiddenPotential"/>) never moves and is only ever approached, not
    /// guaranteed. Past the peak, age bites the physical attributes (pace, acceleration, agility, stamina,
    /// jumping), so a pace-bound winger or full-back declines far more than a positional centre-back or a
    /// keeper — the ratings fall out of that naturally. Deterministic through the injected rng: the same
    /// player and seed develop identically. Constants tune into a BalanceSO later.
    /// </summary>
    public sealed class PlayerDevelopment
    {
        // Growth as a fraction of the remaining ability gap, by age — steep for teenagers, tapering off
        // through the mid-twenties, then a slow trickle into the late twenties for a late developer still
        // short of an unrealised ceiling (CM 01/02: a player keeps inching toward his potential until ~30).
        // Applied to each of the role's rating attributes, so the role rating rises by about this fraction
        // of the gap per season (the weights sum to 1) and never overshoots the ceiling.
        private static double GrowthRate(int age)
        {
            if (age <= 20)
            {
                return 0.14;
            }

            if (age <= 22)
            {
                return 0.10;
            }

            if (age <= 24)
            {
                return 0.07;
            }

            if (age <= 26)
            {
                return 0.045;
            }

            if (age <= 29)
            {
                return 0.02;
            }

            return 0.0;
        }

        // A per-season multiplier so growth and decline are not a smooth mechanical curve: some seasons a
        // player kicks on, some he stalls (CM 01/02's development feel). Centred on 1.0, drawn from the rng
        // so it stays deterministic. The range never lets a single season's growth run past the ceiling.
        private const double MinVariance = 0.6;
        private const double MaxVariance = 1.4;

        private static double SeasonVariance(IRandom rng)
        {
            return MinVariance + (rng.NextDouble() * (MaxVariance - MinVariance));
        }

        // The age a role tends to peak at, before decline sets in — not one number for everyone (CM 01/02):
        // a pacey winger or striker fades first, a positional midfielder or centre-back holds longer, and a
        // keeper lasts longest of all. A per-player offset (below) then shifts this a few years either way.
        private static int RolePeakAge(PlayerRole role)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    return 34;
                case PlayerRole.CentreBack:
                case PlayerRole.DefensiveMidfield:
                case PlayerRole.CentralMidfield:
                case PlayerRole.AttackingMidfield:
                    return 32;
                case PlayerRole.RightBack:
                case PlayerRole.LeftBack:
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    return 31;
                default: // wingers and strikers — the most pace-dependent, so the first to go
                    return 30;
            }
        }

        // A stable per-player shift to the peak age, in [-2, +2] — so two players in the same role do not
        // decline on the same birthday; some last, a few fade early. Derived from the id (a SplitMix64
        // finalizer), not the season rng, so it is the same every season and fully deterministic (and spreads
        // evenly across a pool). A real driver (professionalism / personality) replaces this proxy in Faz 4.
        private static int PeakAgeOffset(PlayerId id)
        {
            ulong z = (ulong)(uint)id.Value * 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z ^= z >> 27;
            return (int)(z % 5UL) - 2;
        }

        // When this player, in this role, starts to decline — never before 30 (physical decline is real by
        // then even for the latest bloomers), otherwise the role peak shifted by his personal offset.
        private static int DeclineOnsetAge(Player player)
        {
            return Math.Max(30, RolePeakAge(player.Role) + PeakAgeOffset(player.Id));
        }

        // Points of ability lost per season past the peak, accelerating with age (capped), so decline is
        // gentle at first and steeper into the mid-thirties.
        private const double DeclinePerYear = 0.9;
        private const int MaxDeclineYears = 6;

        private const byte PhysicalFloor = 15;

        // Floor for the role's rating attributes as age erodes them — an old pro loses a step, not his craft.
        private const byte AttributeFloor = 25;

        // How much of a season's decline hits the role's own attributes (general ability slip) versus the
        // athletic erosion on top. Below 1 so a positional player fades gently and a keeper ages the slowest.
        private const double GeneralDeclineFactor = 0.6;

        /// <summary>
        /// Returns the player one season on: age incremented, and attributes grown toward potential (if
        /// still young and below the ceiling) or worn down physically (if past the peak). Never mutates the
        /// input — a new <see cref="Player"/> is returned; identity, name, role, and hidden potential carry over.
        /// </summary>
        public Player Develop(Player player, IRandom rng)
        {
            Attributes attributes = player.Attributes;

            double growthRate = GrowthRate(player.Age);
            if (growthRate > 0.0)
            {
                double gap = player.HiddenPotential - PlayerRatings.ForRole(player);
                if (gap > 0.0)
                {
                    // Raising each role attribute by this lifts the role rating by about the same amount
                    // (weights sum to 1); capping at the gap keeps the rating from overshooting potential.
                    double perAttribute = Math.Min(growthRate * gap * SeasonVariance(rng), gap);
                    attributes = AdjustRoleAttributes(attributes, player.Role, perAttribute, rng);
                }
            }

            int yearsPastPeak = player.Age - DeclineOnsetAge(player);
            if (yearsPastPeak > 0)
            {
                double amount = DeclinePerYear * Math.Min(yearsPastPeak, MaxDeclineYears) * SeasonVariance(rng);

                // Two-part decline so the OVR actually falls once past this player's own peak, hardest for
                // the pace-reliant. (1) A general erosion of the role's own rating attributes — his overall
                // quality slips, so the role rating drops for every role, keeper or striker.
                attributes = AdjustRoleAttributes(attributes, player.Role, -amount * GeneralDeclineFactor, rng);
                // (2) An extra athletic erosion on the raw physical attributes. Pace and stamina feed the
                // wide and forward ratings, so wingers and full-backs fall off far faster than a positional
                // centre-back or a keeper, whose ratings barely touch them.
                attributes = ApplyDecline(attributes, amount, rng);
            }

            return new Player(player.Id, player.Name, player.Nationality, player.Role, player.Age + 1, attributes, player.HiddenPotential);
        }

        // Adjusts exactly the attributes the role is scored on (mirrors PlayerRatings.ForRole) by a signed
        // amount, so both growth (positive) and the general age erosion (negative) move the role rating and
        // the scout's key stats — not numbers no one reads.
        private static Attributes AdjustRoleAttributes(Attributes a, PlayerRole role, double amount, IRandom rng)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    a.Reflexes = Adjust(a.Reflexes, amount, rng);
                    a.Handling = Adjust(a.Handling, amount, rng);
                    a.OneOnOnes = Adjust(a.OneOnOnes, amount, rng);
                    a.CommandOfArea = Adjust(a.CommandOfArea, amount, rng);
                    a.AerialReach = Adjust(a.AerialReach, amount, rng);
                    a.GkPositioning = Adjust(a.GkPositioning, amount, rng);
                    break;
                case PlayerRole.CentreBack:
                    a.Marking = Adjust(a.Marking, amount, rng);
                    a.Tackling = Adjust(a.Tackling, amount, rng);
                    a.Heading = Adjust(a.Heading, amount, rng);
                    a.Strength = Adjust(a.Strength, amount, rng);
                    a.Positioning = Adjust(a.Positioning, amount, rng);
                    break;
                case PlayerRole.RightBack:
                case PlayerRole.LeftBack:
                    a.Pace = Adjust(a.Pace, amount, rng);
                    a.Crossing = Adjust(a.Crossing, amount, rng);
                    a.Tackling = Adjust(a.Tackling, amount, rng);
                    a.Marking = Adjust(a.Marking, amount, rng);
                    a.Stamina = Adjust(a.Stamina, amount, rng);
                    a.Positioning = Adjust(a.Positioning, amount, rng);
                    break;
                case PlayerRole.DefensiveMidfield:
                    a.Tackling = Adjust(a.Tackling, amount, rng);
                    a.Marking = Adjust(a.Marking, amount, rng);
                    a.Positioning = Adjust(a.Positioning, amount, rng);
                    a.Passing = Adjust(a.Passing, amount, rng);
                    a.Stamina = Adjust(a.Stamina, amount, rng);
                    break;
                case PlayerRole.CentralMidfield:
                    a.Passing = Adjust(a.Passing, amount, rng);
                    a.Technique = Adjust(a.Technique, amount, rng);
                    a.FirstTouch = Adjust(a.FirstTouch, amount, rng);
                    a.Positioning = Adjust(a.Positioning, amount, rng);
                    a.Stamina = Adjust(a.Stamina, amount, rng);
                    break;
                case PlayerRole.AttackingMidfield:
                    a.Passing = Adjust(a.Passing, amount, rng);
                    a.Technique = Adjust(a.Technique, amount, rng);
                    a.Dribbling = Adjust(a.Dribbling, amount, rng);
                    a.FirstTouch = Adjust(a.FirstTouch, amount, rng);
                    a.LongShots = Adjust(a.LongShots, amount, rng);
                    break;
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    a.Crossing = Adjust(a.Crossing, amount, rng);
                    a.Pace = Adjust(a.Pace, amount, rng);
                    a.Stamina = Adjust(a.Stamina, amount, rng);
                    a.Passing = Adjust(a.Passing, amount, rng);
                    a.Dribbling = Adjust(a.Dribbling, amount, rng);
                    break;
                case PlayerRole.RightWing:
                case PlayerRole.LeftWing:
                    a.Pace = Adjust(a.Pace, amount, rng);
                    a.Dribbling = Adjust(a.Dribbling, amount, rng);
                    a.Crossing = Adjust(a.Crossing, amount, rng);
                    a.Technique = Adjust(a.Technique, amount, rng);
                    a.Finishing = Adjust(a.Finishing, amount, rng);
                    break;
                default: // Striker
                    a.Finishing = Adjust(a.Finishing, amount, rng);
                    a.Positioning = Adjust(a.Positioning, amount, rng);
                    a.Pace = Adjust(a.Pace, amount, rng);
                    a.Technique = Adjust(a.Technique, amount, rng);
                    a.Heading = Adjust(a.Heading, amount, rng);
                    break;
            }

            return a;
        }

        // Age wears the athletic attributes; technical and positional stay, which is why a keeper or a
        // reading-the-game centre-back ages gracefully while a winger who lived on pace falls off a cliff.
        private static Attributes ApplyDecline(Attributes a, double amount, IRandom rng)
        {
            a.Pace = Lower(a.Pace, amount, rng);
            a.Acceleration = Lower(a.Acceleration, amount, rng);
            a.Agility = Lower(a.Agility, amount, rng);
            a.Stamina = Lower(a.Stamina, amount, rng);
            a.Jumping = Lower(a.Jumping, amount, rng);
            return a;
        }

        // Applies a signed amount to an attribute: its magnitude becomes a whole-number step (the rng
        // decides the leftover fraction, so change is rng-varied), added when growing or subtracted when
        // eroding. Capped at 99 on the way up and floored on the way down.
        private static byte Adjust(byte value, double amount, IRandom rng)
        {
            int step = WholeStep(Math.Abs(amount), rng);
            int result = amount >= 0.0 ? value + step : value - step;
            if (result > 99)
            {
                result = 99;
            }

            if (result < AttributeFloor)
            {
                result = AttributeFloor;
            }

            return (byte)result;
        }

        private static byte Lower(byte value, double amount, IRandom rng)
        {
            int step = WholeStep(amount, rng);
            int lowered = value - step;
            return (byte)(lowered < PhysicalFloor ? PhysicalFloor : lowered);
        }

        private static int WholeStep(double amount, IRandom rng)
        {
            int whole = (int)amount;
            double fraction = amount - whole;
            return whole + (rng.NextDouble() < fraction ? 1 : 0);
        }
    }
}
