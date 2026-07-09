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

        // Physical points lost per season once past the peak — nothing until 30, then rising with age.
        private static double DeclineAmount(int age)
        {
            if (age <= 29)
            {
                return 0.0;
            }

            if (age <= 31)
            {
                return 1.5;
            }

            if (age <= 33)
            {
                return 2.5;
            }

            return 3.5;
        }

        private const byte PhysicalFloor = 15;

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
                    attributes = ApplyGrowth(attributes, player.Role, perAttribute, rng);
                }
            }

            double decline = DeclineAmount(player.Age);
            if (decline > 0.0)
            {
                attributes = ApplyDecline(attributes, decline * SeasonVariance(rng), rng);
            }

            return new Player(player.Id, player.Name, player.Nationality, player.Role, player.Age + 1, attributes, player.HiddenPotential);
        }

        // Grows exactly the attributes the role is scored on (mirrors PlayerRatings.ForRole), so improvement
        // shows up in the role rating and in the scout's key stats, not in numbers no one reads.
        private static Attributes ApplyGrowth(Attributes a, PlayerRole role, double amount, IRandom rng)
        {
            switch (role)
            {
                case PlayerRole.Goalkeeper:
                    a.Reflexes = Raise(a.Reflexes, amount, rng);
                    a.Handling = Raise(a.Handling, amount, rng);
                    a.OneOnOnes = Raise(a.OneOnOnes, amount, rng);
                    a.CommandOfArea = Raise(a.CommandOfArea, amount, rng);
                    a.AerialReach = Raise(a.AerialReach, amount, rng);
                    a.GkPositioning = Raise(a.GkPositioning, amount, rng);
                    break;
                case PlayerRole.CentreBack:
                    a.Marking = Raise(a.Marking, amount, rng);
                    a.Tackling = Raise(a.Tackling, amount, rng);
                    a.Heading = Raise(a.Heading, amount, rng);
                    a.Strength = Raise(a.Strength, amount, rng);
                    a.Positioning = Raise(a.Positioning, amount, rng);
                    break;
                case PlayerRole.RightBack:
                case PlayerRole.LeftBack:
                    a.Pace = Raise(a.Pace, amount, rng);
                    a.Crossing = Raise(a.Crossing, amount, rng);
                    a.Tackling = Raise(a.Tackling, amount, rng);
                    a.Marking = Raise(a.Marking, amount, rng);
                    a.Stamina = Raise(a.Stamina, amount, rng);
                    a.Positioning = Raise(a.Positioning, amount, rng);
                    break;
                case PlayerRole.DefensiveMidfield:
                    a.Tackling = Raise(a.Tackling, amount, rng);
                    a.Marking = Raise(a.Marking, amount, rng);
                    a.Positioning = Raise(a.Positioning, amount, rng);
                    a.Passing = Raise(a.Passing, amount, rng);
                    a.Stamina = Raise(a.Stamina, amount, rng);
                    break;
                case PlayerRole.CentralMidfield:
                    a.Passing = Raise(a.Passing, amount, rng);
                    a.Technique = Raise(a.Technique, amount, rng);
                    a.FirstTouch = Raise(a.FirstTouch, amount, rng);
                    a.Positioning = Raise(a.Positioning, amount, rng);
                    a.Stamina = Raise(a.Stamina, amount, rng);
                    break;
                case PlayerRole.AttackingMidfield:
                    a.Passing = Raise(a.Passing, amount, rng);
                    a.Technique = Raise(a.Technique, amount, rng);
                    a.Dribbling = Raise(a.Dribbling, amount, rng);
                    a.FirstTouch = Raise(a.FirstTouch, amount, rng);
                    a.LongShots = Raise(a.LongShots, amount, rng);
                    break;
                case PlayerRole.RightMidfield:
                case PlayerRole.LeftMidfield:
                    a.Crossing = Raise(a.Crossing, amount, rng);
                    a.Pace = Raise(a.Pace, amount, rng);
                    a.Stamina = Raise(a.Stamina, amount, rng);
                    a.Passing = Raise(a.Passing, amount, rng);
                    a.Dribbling = Raise(a.Dribbling, amount, rng);
                    break;
                case PlayerRole.RightWing:
                case PlayerRole.LeftWing:
                    a.Pace = Raise(a.Pace, amount, rng);
                    a.Dribbling = Raise(a.Dribbling, amount, rng);
                    a.Crossing = Raise(a.Crossing, amount, rng);
                    a.Technique = Raise(a.Technique, amount, rng);
                    a.Finishing = Raise(a.Finishing, amount, rng);
                    break;
                default: // Striker
                    a.Finishing = Raise(a.Finishing, amount, rng);
                    a.Positioning = Raise(a.Positioning, amount, rng);
                    a.Pace = Raise(a.Pace, amount, rng);
                    a.Technique = Raise(a.Technique, amount, rng);
                    a.Heading = Raise(a.Heading, amount, rng);
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

        // A fractional amount becomes a whole-number step, with the rng deciding the leftover fraction — so
        // growth is rng-varied yet never negative (each role attribute is non-decreasing → the rating grows).
        private static byte Raise(byte value, double amount, IRandom rng)
        {
            int step = WholeStep(amount, rng);
            int raised = value + step;
            return (byte)(raised > 99 ? 99 : raised);
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
