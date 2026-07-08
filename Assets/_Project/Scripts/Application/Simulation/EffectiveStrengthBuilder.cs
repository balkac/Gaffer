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
                double defence = DefenceRating(attributes);

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

        private static double AttackRating(Attributes a)
        {
            return (0.5 * a.Finishing) + (0.3 * a.Pace) + (0.2 * a.Positioning);
        }

        private static double MidfieldRating(Attributes a)
        {
            return (0.5 * a.Passing) + (0.3 * a.Positioning) + (0.2 * a.Stamina);
        }

        private static double DefenceRating(Attributes a)
        {
            return (0.6 * a.Tackling) + (0.4 * a.Positioning);
        }

        private static double LineAverage(double lineTotal, int lineCount, double squadTotal, int squadCount)
        {
            return lineCount > 0 ? lineTotal / lineCount : squadTotal / squadCount;
        }
    }
}
