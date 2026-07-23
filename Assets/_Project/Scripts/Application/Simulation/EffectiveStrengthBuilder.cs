using System.Collections.Generic;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using Gaffer.Domain.Traits;

namespace Gaffer.Application.Simulation
{
    /// <summary>
    /// Derives a match <see cref="TeamStrength"/> from the players who take the field — the bridge that
    /// connects the starting eleven to the simulation (BuildEffectiveStrength, TDD §6.1). Each axis is the
    /// average of each player's own role rating over the players who man that line: attack from the forwards,
    /// midfield from the midfielders, defence from the defenders and goalkeeper. Because the rating is
    /// role-specific (a full-back weighed on pace and crossing, a centre-back on marking and heading), an
    /// attacking full-back and a stopper both lift the defence axis by what they are actually good at. An
    /// empty line falls back to the whole-lineup average of that rating, so the result is always plausible
    /// and never divides by zero. Traits modulate each player's rating here — the step that binds character
    /// to the match (TDD §6): a context trait answers the stakes in the <see cref="MatchContext"/>, and a
    /// leader's aura lifts every teammate. Tactics shift the axes through the injected
    /// <see cref="TacticsSettings"/> (data-driven, NON-NEGOTIABLE #3); form follows.
    /// </summary>
    public sealed class EffectiveStrengthBuilder
    {
        private readonly TraitCatalog _traits;
        private readonly TacticsSettings _tactics;

        public EffectiveStrengthBuilder()
            : this(TraitCatalog.Default, TacticsSettings.Default)
        {
        }

        public EffectiveStrengthBuilder(TraitCatalog traits)
            : this(traits, TacticsSettings.Default)
        {
        }

        /// <summary>Builds with specific tactics balance (from a config asset) shaping how far mentality
        /// and pressing bend the axes. Null falls back to the calibrated defaults.</summary>
        public EffectiveStrengthBuilder(TraitCatalog traits, TacticsSettings tactics)
        {
            _traits = traits;
            _tactics = tactics ?? TacticsSettings.Default;
        }

        public TeamStrength Build(Squad squad)
        {
            return Build(squad.Players, Tactics.Balanced, default);
        }

        public TeamStrength Build(Squad squad, Tactics tactics)
        {
            return Build(squad.Players, tactics, default);
        }

        public TeamStrength Build(IReadOnlyList<Player> players, Tactics tactics)
        {
            return Build(players, tactics, default);
        }

        public TeamStrength Build(IReadOnlyList<Player> players, Tactics tactics, MatchContext context)
        {
            return Build(players, tactics, context, null);
        }

        /// <summary>Builds with a per-player condition source (morale today, form later) scaling each
        /// rating — how off-pitch drama is felt on the pitch. Null means everyone at 1.0.</summary>
        public TeamStrength Build(IReadOnlyList<Player> players, Tactics tactics, MatchContext context, IPlayerConditionSource condition)
        {
            if (players.Count == 0)
            {
                return new TeamStrength(0.0, 0.0, 0.0);
            }

            // A leader's aura reaches his teammates, not himself: the combined product over the lineup is
            // divided back out per player, so fielding the leader is felt by the other ten.
            double lineupAura = 1.0;
            for (int i = 0; i < players.Count; i++)
            {
                lineupAura *= AuraOf(players[i]);
            }

            double lineupTotal = 0.0;

            double forwardTotal = 0.0;
            int forwardCount = 0;
            double midfielderTotal = 0.0;
            int midfielderCount = 0;
            double defensiveTotal = 0.0;
            int defensiveCount = 0;

            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];

                // Each player is scored on his own role, then contributes that single rating to the axis of
                // the line he mans — no cross-axis scoring, so a role's rating means the same thing everywhere.
                double rating = PlayerRatings.ForRole(player)
                    * ContextMultiplier(player, context)
                    * (lineupAura / AuraOf(player))
                    * (condition?.RatingMultiplierOf(player.Id) ?? 1.0);
                lineupTotal += rating;

                switch (player.Position)
                {
                    case Position.Forward:
                        forwardTotal += rating;
                        forwardCount++;
                        break;
                    case Position.Midfielder:
                        midfielderTotal += rating;
                        midfielderCount++;
                        break;
                    case Position.Defender:
                    case Position.Goalkeeper:
                        defensiveTotal += rating;
                        defensiveCount++;
                        break;
                }
            }

            double attackAxis = LineAverage(forwardTotal, forwardCount, lineupTotal, players.Count);
            double midfieldAxis = LineAverage(midfielderTotal, midfielderCount, lineupTotal, players.Count);
            double defenceAxis = LineAverage(defensiveTotal, defensiveCount, lineupTotal, players.Count);

            return ApplyTactics(attackAxis, midfieldAxis, defenceAxis, tactics);
        }

        // Mentality and pressing shift the base axes multiplicatively; a Balanced setup (scales 0) is the
        // identity. Attacking mentality trades defence for attack; a high press wins the midfield but
        // exposes the line. Tempo and approach do not touch strength — they shape the ChanceProfile
        // instead, so each axis stays mechanically distinct. The step sizes come from TacticsSettings.
        private TeamStrength ApplyTactics(double attack, double midfield, double defence, Tactics tactics)
        {
            int mentality = tactics.MentalityScale;
            int pressing = tactics.PressingScale;

            double attackMult = 1.0 + (_tactics.MentalityAttackStep * mentality);
            double midfieldMult = 1.0 + (_tactics.PressingMidfieldStep * pressing);
            double defenceMult = (1.0 - (_tactics.MentalityDefenceStep * mentality)) * (1.0 - (_tactics.PressingDefenceStep * pressing));

            return new TeamStrength(attack * attackMult, midfield * midfieldMult, defence * defenceMult);
        }

        private static double LineAverage(double lineTotal, int lineCount, double lineupTotal, int lineupCount)
        {
            return lineCount > 0 ? lineTotal / lineCount : lineupTotal / lineupCount;
        }

        // The product of this player's context-conditional trait multipliers for these stakes — a derby
        // beast over 1, a bottler under, everyone exactly 1.0 in a plain fixture, so a trait is inert
        // where its occasion is absent and measurably real where it is (NON-NEGOTIABLE #7).
        private double ContextMultiplier(Player player, in MatchContext context)
        {
            double multiplier = 1.0;
            IReadOnlyList<TraitId> traits = player.Traits;
            for (int i = 0; i < traits.Count; i++)
            {
                Trait trait = _traits.Find(traits[i]);
                if (trait != null && Applies(trait.Match, context))
                {
                    multiplier *= trait.Match.Multiplier;
                }
            }

            return multiplier;
        }

        // Evaluates the pure stake flags against the sim's context here, in the application layer, so the
        // domain's trait data never references simulation types (arrows point inward).
        private static bool Applies(in MatchTraitModifier modifier, in MatchContext context)
        {
            MatchStakes stakes = modifier.Stakes;
            if (stakes == MatchStakes.None)
            {
                return false;
            }

            if ((stakes & MatchStakes.Derby) != 0 && context.Importance == MatchImportance.Derby)
            {
                return true;
            }

            if ((stakes & MatchStakes.Final) != 0 && context.Importance == MatchImportance.Final)
            {
                return true;
            }

            if ((stakes & MatchStakes.RelegationSixPointer) != 0 && context.Importance == MatchImportance.RelegationSixPointer)
            {
                return true;
            }

            if ((stakes & MatchStakes.Rivalry) != 0 && context.IsRivalry)
            {
                return true;
            }

            if ((stakes & MatchStakes.TitleDecider) != 0 && context.IsTitleDecider)
            {
                return true;
            }

            if ((stakes & MatchStakes.BigCrowd) != 0 && modifier.BigCrowdThreshold > 0 && context.CrowdSize >= modifier.BigCrowdThreshold)
            {
                return true;
            }

            return false;
        }

        private double AuraOf(Player player)
        {
            double aura = 1.0;
            IReadOnlyList<TraitId> traits = player.Traits;
            for (int i = 0; i < traits.Count; i++)
            {
                Trait trait = _traits.Find(traits[i]);
                if (trait != null)
                {
                    aura *= trait.TeammateAura;
                }
            }

            return aura;
        }
    }
}
