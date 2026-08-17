using System;
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
    /// and never divides by zero.
    ///
    /// <para><b>The line a player mans is the SLOT's, not his own.</b> Given a team sheet
    /// (<see cref="SlottedPlayer"/>), a centre-back pushed up front lifts the attack — badly, at the
    /// out-of-position rate (<see cref="PositionalFitSettings"/>) — and stops propping up the defence he is
    /// no longer in. Reading his own line instead made the formation decorative: eleven defenders in a 4-3-3
    /// scored an ordinary attack axis off the empty-line fallback, so a manager could field a back eleven and
    /// the simulation would never notice. Given a bare list of players with no sheet, every man is taken to
    /// be in his own role — which is what an unshaped squad's strength means, and costs nothing.</para>
    ///
    /// <para>Traits modulate each player's rating here — the step that binds character
    /// to the match (TDD §6): a context trait answers the stakes in the <see cref="MatchContext"/>, and a
    /// leader's aura lifts every teammate. Tactics shift the axes through the injected
    /// <see cref="TacticsSettings"/> (data-driven, NON-NEGOTIABLE #3); form follows.
    /// </summary>
    public sealed class EffectiveStrengthBuilder
    {
        // A trait's teammate aura is both a factor of the lineup product and the divisor that takes a
        // player back out of it, so a zero (or a negative, or a NaN from a half-authored asset) would
        // not raise anything — it would make 0/0 and poison every axis, and NaN then makes every
        // downstream `if (x < limit)` take the wrong branch (CONVENTIONS §6). Clamping the factor at
        // the boundary keeps the divisor strictly positive. The band is far wider than any authored
        // aura (the calibrated catalog's only one is 1.03), so no calibrated result moves.
        private const double MinAuraFactor = 0.01;
        private const double MaxAuraFactor = 100.0;

        private readonly TraitCatalog _traits;
        private readonly TacticsSettings _tactics;
        private readonly PositionalFitSettings _fit;

        // Scratch sheet for the overloads that are handed a bare list of players: each is paired with his
        // own role, which is a Natural fit and therefore free, and the one loop below runs over that. One
        // code path, so "which line does this man?" and "what does his position cost?" cannot grow two
        // answers (ARCHITECTURE §8a). Reused rather than allocated per call — this runs for every club
        // every week (PERFORMANCE §8) — and safe because the core is single-threaded and Build never
        // re-enters itself.
        private readonly List<SlottedPlayer> _ownRoleSheet = new List<SlottedPlayer>(16);

        public EffectiveStrengthBuilder()
            : this(TraitCatalog.Default, TacticsSettings.Default)
        {
        }

        public EffectiveStrengthBuilder(TraitCatalog traits)
            : this(traits, TacticsSettings.Default)
        {
        }

        /// <summary>Builds with specific tactics balance (from a config asset) shaping how far mentality
        /// and pressing bend the axes, and what playing a man out of position costs. Null falls back to
        /// the calibrated defaults.</summary>
        public EffectiveStrengthBuilder(TraitCatalog traits, TacticsSettings tactics, PositionalFitSettings fit = null)
        {
            _traits = traits;
            _tactics = tactics ?? TacticsSettings.Default;
            _fit = fit ?? PositionalFitSettings.Default;
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
            List<SlottedPlayer> sheet = _ownRoleSheet;
            sheet.Clear();
            for (int i = 0; i < players.Count; i++)
            {
                Player player = players[i];
                sheet.Add(new SlottedPlayer(i, player.Role, player));
            }

            return Build(sheet, tactics, context, condition);
        }

        /// <summary>Builds from a team sheet, so each man is judged on the slot he actually stands in.</summary>
        public TeamStrength Build(IReadOnlyList<SlottedPlayer> sheet, Tactics tactics)
        {
            return Build(sheet, tactics, default, null);
        }

        /// <summary>
        /// Builds from a team sheet with stakes and condition — the full form, and the one the played
        /// season goes through.
        /// </summary>
        public TeamStrength Build(IReadOnlyList<SlottedPlayer> sheet, Tactics tactics, MatchContext context, IPlayerConditionSource condition)
        {
            if (sheet.Count == 0)
            {
                return new TeamStrength(0.0, 0.0, 0.0);
            }

            // A leader's aura reaches his teammates, not himself: the combined product over the lineup is
            // divided back out per player, so fielding the leader is felt by the other ten.
            double lineupAura = 1.0;
            for (int i = 0; i < sheet.Count; i++)
            {
                lineupAura *= AuraOf(sheet[i].Player);
            }

            double lineupTotal = 0.0;

            double forwardTotal = 0.0;
            int forwardCount = 0;
            double midfielderTotal = 0.0;
            int midfielderCount = 0;
            double defensiveTotal = 0.0;
            int defensiveCount = 0;

            for (int i = 0; i < sheet.Count; i++)
            {
                SlottedPlayer entry = sheet[i];
                Player player = entry.Player;

                // Scored on his own role and charged for the slot, then contributes that single rating to the
                // axis of the line the SLOT sits on — no cross-axis scoring, so a role's rating means the
                // same thing everywhere and the shape on the team sheet is the shape the match plays.
                double rating = PlayerRatings.ForSlot(player, entry.Role, _fit)
                    * ContextMultiplier(player, context)
                    * (lineupAura / AuraOf(player))
                    * (condition?.RatingMultiplierOf(player.Id) ?? 1.0);
                lineupTotal += rating;

                switch (PlayerRoles.Line(entry.Role))
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
                    default:
                        // A new Position with no axis here is a broken invariant, not a recoverable
                        // outcome (CONVENTIONS §1/§4): it would still land in lineupTotal, so it would
                        // silently dilute attack, midfield and defence in every match ever played.
                        throw new ArgumentOutOfRangeException(
                            nameof(sheet),
                            PlayerRoles.Line(entry.Role),
                            $"Slot role '{entry.Role}' mans no line, so it contributes to no strength axis.");
                }
            }

            double attackAxis = LineAverage(forwardTotal, forwardCount, lineupTotal, sheet.Count);
            double midfieldAxis = LineAverage(midfielderTotal, midfielderCount, lineupTotal, sheet.Count);
            double defenceAxis = LineAverage(defensiveTotal, defensiveCount, lineupTotal, sheet.Count);

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
                    aura *= ClampAuraFactor(trait.TeammateAura);
                }
            }

            return aura;
        }

        // Written as a failed lower-bound test rather than `factor < MinAuraFactor` so a NaN factor
        // lands on the floor too — every comparison with NaN is false, so the naive form would let it
        // straight through (CONVENTIONS §6).
        private static double ClampAuraFactor(double factor)
        {
            if (!(factor >= MinAuraFactor))
            {
                return MinAuraFactor;
            }

            return factor > MaxAuraFactor ? MaxAuraFactor : factor;
        }
    }
}
