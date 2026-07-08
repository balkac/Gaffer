using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Derives a squad's match <see cref="TeamStrength"/> from its players — the bridge that connects
    /// generated players to the simulation (BuildEffectiveStrength, TDD §6.1). Each axis is the average
    /// of the relevant role rating over the players who man that line: attack from the forwards, midfield
    /// from the midfielders, defence from the defenders and goalkeeper. An empty line falls back to the
    /// squad-wide average of that rating, so the result is always plausible and never divides by zero.
    /// Tactics, form, and traits shift these axes in later steps; the weights tune into a BalanceSO then.
    /// </summary>
    public sealed class EffectiveStrengthBuilder
    {
        public TeamStrength Build(Squad squad)
        {
            IReadOnlyList<Player> players = squad.Players;
            if (players.Count == 0)
            {
                return new TeamStrength(0.0, 0.0, 0.0);
            }

            double squadAttack = 0.0;
            double squadMidfield = 0.0;
            double squadDefence = 0.0;

            double forwardAttack = 0.0;
            int forwardCount = 0;
            double midfielderMidfield = 0.0;
            int midfielderCount = 0;
            double defensiveDefence = 0.0;
            int defensiveCount = 0;

            foreach (Player player in players)
            {
                Attributes attributes = player.Attributes;
                double attack = AttackRating(attributes);
                double midfield = MidfieldRating(attributes);
                double defence = DefenceRating(player.Position, attributes);

                squadAttack += attack;
                squadMidfield += midfield;
                squadDefence += defence;

                switch (player.Position)
                {
                    case Position.Forward:
                        forwardAttack += attack;
                        forwardCount++;
                        break;
                    case Position.Midfielder:
                        midfielderMidfield += midfield;
                        midfielderCount++;
                        break;
                    case Position.Defender:
                    case Position.Goalkeeper:
                        defensiveDefence += defence;
                        defensiveCount++;
                        break;
                }
            }

            double attackAxis = LineAverage(forwardAttack, forwardCount, squadAttack, players.Count);
            double midfieldAxis = LineAverage(midfielderMidfield, midfielderCount, squadMidfield, players.Count);
            double defenceAxis = LineAverage(defensiveDefence, defensiveCount, squadDefence, players.Count);

            return new TeamStrength(attackAxis, midfieldAxis, defenceAxis);
        }

        // Each rating weights a role's key attributes (see RoleKeyAttributes); weights sum to 1 so the
        // axis stays on the 0–100 attribute scale. Defence branches on the role: a keeper is rated on the
        // keeping group, an outfielder on tackling, marking, and heading.
        private static double AttackRating(Attributes a)
        {
            return (0.35 * a.Finishing) + (0.20 * a.Pace) + (0.20 * a.Technique) + (0.15 * a.Positioning) + (0.10 * a.Dribbling);
        }

        private static double MidfieldRating(Attributes a)
        {
            return (0.30 * a.Passing) + (0.25 * a.Technique) + (0.15 * a.FirstTouch) + (0.15 * a.Positioning) + (0.15 * a.Stamina);
        }

        private static double DefenceRating(Position position, Attributes a)
        {
            if (position == Position.Goalkeeper)
            {
                return (0.30 * a.Reflexes) + (0.20 * a.Handling) + (0.20 * a.OneOnOnes) + (0.15 * a.CommandOfArea) + (0.10 * a.AerialReach) + (0.05 * a.GkPositioning);
            }

            return (0.30 * a.Tackling) + (0.30 * a.Marking) + (0.15 * a.Heading) + (0.15 * a.Strength) + (0.10 * a.Positioning);
        }

        private static double LineAverage(double lineTotal, int lineCount, double squadTotal, int squadCount)
        {
            return lineCount > 0 ? lineTotal / lineCount : squadTotal / squadCount;
        }
    }
}
