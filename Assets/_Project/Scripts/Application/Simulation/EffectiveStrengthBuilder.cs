using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Derives a match <see cref="TeamStrength"/> from the players who take the field — the bridge that
    /// connects the starting eleven to the simulation (BuildEffectiveStrength, TDD §6.1). Each axis is the
    /// average of the relevant role rating over the players who man that line: attack from the forwards,
    /// midfield from the midfielders, defence from the defenders and goalkeeper. An empty line falls back
    /// to the whole-lineup average of that rating, so the result is always plausible and never divides by
    /// zero. Tactics shift the axes; form and traits follow. The weights tune into a BalanceSO then.
    /// </summary>
    public sealed class EffectiveStrengthBuilder
    {
        public TeamStrength Build(Squad squad)
        {
            return Build(squad.Players, Tactics.Balanced);
        }

        public TeamStrength Build(Squad squad, Tactics tactics)
        {
            return Build(squad.Players, tactics);
        }

        public TeamStrength Build(IReadOnlyList<Player> players, Tactics tactics)
        {
            if (players.Count == 0)
            {
                return new TeamStrength(0.0, 0.0, 0.0);
            }

            double lineupAttack = 0.0;
            double lineupMidfield = 0.0;
            double lineupDefence = 0.0;

            double forwardAttack = 0.0;
            int forwardCount = 0;
            double midfielderMidfield = 0.0;
            int midfielderCount = 0;
            double defensiveDefence = 0.0;
            int defensiveCount = 0;

            foreach (Player player in players)
            {
                Attributes attributes = player.Attributes;
                double attack = PlayerRatings.Attack(attributes);
                double midfield = PlayerRatings.Midfield(attributes);
                double defence = PlayerRatings.Defence(player.Position, attributes);

                lineupAttack += attack;
                lineupMidfield += midfield;
                lineupDefence += defence;

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

            double attackAxis = LineAverage(forwardAttack, forwardCount, lineupAttack, players.Count);
            double midfieldAxis = LineAverage(midfielderMidfield, midfielderCount, lineupMidfield, players.Count);
            double defenceAxis = LineAverage(defensiveDefence, defensiveCount, lineupDefence, players.Count);

            return ApplyTactics(attackAxis, midfieldAxis, defenceAxis, tactics);
        }

        // Mentality and pressing shift the base axes multiplicatively; a Balanced setup (scales 0) is the
        // identity. Attacking mentality trades defence for attack; a high press wins the midfield but
        // exposes the line. Tempo and approach do not touch strength — they shape the ChanceProfile
        // instead, so each axis stays mechanically distinct. Weights tune into a BalanceSO later.
        private static TeamStrength ApplyTactics(double attack, double midfield, double defence, Tactics tactics)
        {
            int mentality = tactics.MentalityScale;
            int pressing = tactics.PressingScale;

            double attackMult = 1.0 + (0.09 * mentality);
            double midfieldMult = 1.0 + (0.09 * pressing);
            double defenceMult = (1.0 - (0.07 * mentality)) * (1.0 - (0.04 * pressing));

            return new TeamStrength(attack * attackMult, midfield * midfieldMult, defence * defenceMult);
        }

        private static double LineAverage(double lineTotal, int lineCount, double lineupTotal, int lineupCount)
        {
            return lineCount > 0 ? lineTotal / lineCount : lineupTotal / lineupCount;
        }
    }
}
