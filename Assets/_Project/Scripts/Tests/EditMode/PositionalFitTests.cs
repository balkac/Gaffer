using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// Playing a man out of position, asserted end to end: the classification, its price, and — the part
    /// that was missing entirely — that the shape on the team sheet is the shape the match plays.
    ///
    /// <para><b>What this replaces.</b> The squad screen marked a bench player as suited or not to a slot,
    /// and the simulation charged nothing for ignoring it: <c>EffectiveStrengthBuilder</c> rated every man
    /// on his own role and added him to the axis of his OWN line, so a centre-back put up front went on
    /// propping up the defence and the attack was scored off an empty-line fallback. A manager could field
    /// eleven defenders in a 4-3-3 and the league would not notice — which makes the indicator flavour text,
    /// and NON-NEGOTIABLE #7 does not allow flavour text to look like a mechanic.</para>
    ///
    /// <para>The numbers come from Football Manager's measured penalty (see
    /// <see cref="PositionalFitSettings"/>), so what is pinned here is the shape of the rule — natural is
    /// free, further is worse, the floor is survivable — not a taste.</para>
    /// </summary>
    public sealed class PositionalFitTests
    {
        [Test]
        public void FitFor_HisOwnRole_IsNatural()
        {
            Assert.That(PlayerRoles.FitFor(PlayerRole.CentreBack, PlayerRole.CentreBack), Is.EqualTo(PositionalFit.Natural));
        }

        [Test]
        public void FitFor_AnotherRoleOnTheSameLine_IsSameLine()
        {
            Assert.That(PlayerRoles.FitFor(PlayerRole.CentralMidfield, PlayerRole.RightMidfield), Is.EqualTo(PositionalFit.SameLine));
            Assert.That(PlayerRoles.FitFor(PlayerRole.RightBack, PlayerRole.CentreBack), Is.EqualTo(PositionalFit.SameLine));

            // Wingers sit on the forward line and wide midfielders on the midfield one (PlayerRoles.Line) —
            // the split that keeps a 4-4-2 and a 4-3-3 different shapes, and it holds here too.
            Assert.That(PlayerRoles.FitFor(PlayerRole.RightWing, PlayerRole.Striker), Is.EqualTo(PositionalFit.SameLine));
        }

        [Test]
        public void FitFor_OneLineAway_IsAdjacent_AndTwoIsDistant()
        {
            Assert.That(PlayerRoles.FitFor(PlayerRole.CentreBack, PlayerRole.CentralMidfield), Is.EqualTo(PositionalFit.AdjacentLine));
            Assert.That(PlayerRoles.FitFor(PlayerRole.Striker, PlayerRole.CentralMidfield), Is.EqualTo(PositionalFit.AdjacentLine));
            Assert.That(PlayerRoles.FitFor(PlayerRole.CentreBack, PlayerRole.Striker), Is.EqualTo(PositionalFit.DistantLine));
            Assert.That(PlayerRoles.FitFor(PlayerRole.Striker, PlayerRole.LeftBack), Is.EqualTo(PositionalFit.DistantLine));
        }

        [Test]
        public void FitFor_KeeperAndOutfielder_IsImpossibleInBothDirections()
        {
            // Not merely "far": the distance from goal to defence is one line, and a keeper at centre-back is
            // not a mild compromise. The pair is disqualified before the distance is measured.
            Assert.That(PlayerRoles.FitFor(PlayerRole.Goalkeeper, PlayerRole.CentreBack), Is.EqualTo(PositionalFit.Impossible));
            Assert.That(PlayerRoles.FitFor(PlayerRole.CentreBack, PlayerRole.Goalkeeper), Is.EqualTo(PositionalFit.Impossible));
            Assert.That(PlayerRoles.FitFor(PlayerRole.Striker, PlayerRole.Goalkeeper), Is.EqualTo(PositionalFit.Impossible));
        }

        [Test]
        public void Multipliers_GetWorseWithDistance_AndNeverEraseAPlayer()
        {
            PositionalFitSettings fit = PositionalFitSettings.Default;

            Assert.That(fit.MultiplierFor(PositionalFit.Natural), Is.EqualTo(1.0));
            Assert.That(fit.MultiplierFor(PositionalFit.SameLine), Is.LessThan(1.0));
            Assert.That(fit.MultiplierFor(PositionalFit.AdjacentLine), Is.LessThan(fit.MultiplierFor(PositionalFit.SameLine)));
            Assert.That(fit.MultiplierFor(PositionalFit.DistantLine), Is.LessThan(fit.MultiplierFor(PositionalFit.AdjacentLine)));
            Assert.That(fit.MultiplierFor(PositionalFit.Impossible), Is.LessThan(fit.MultiplierFor(PositionalFit.DistantLine)));

            // FM's own formula bottoms out near 0.57 and its worst tier still wins matches. A misplaced name
            // must read as a cost, not as an injury crisis.
            Assert.That(fit.MultiplierFor(PositionalFit.Impossible), Is.GreaterThan(0.5));
        }

        [Test]
        public void ForSlot_OutOfPosition_RatesBelowTheSameManInHisOwnRole()
        {
            Player defender = Outfielder(1, PlayerRole.CentreBack, 80);

            double own = PlayerRatings.ForSlot(defender, PlayerRole.CentreBack, PositionalFitSettings.Default);
            double upFront = PlayerRatings.ForSlot(defender, PlayerRole.Striker, PositionalFitSettings.Default);

            Assert.That(own, Is.EqualTo(PlayerRatings.ForRole(defender)).Within(1e-9));
            Assert.That(upFront, Is.LessThan(own));

            // The base is his OWN role's rating, charged — not a re-reading of him as a striker. Rating a
            // centre-back on a striker's attributes would ask a question the attribute sheet cannot answer,
            // and would make a quick defender look like a winger.
            Assert.That(upFront, Is.EqualTo(own * PositionalFitSettings.Default.DistantLine).Within(1e-9));
        }

        [Test]
        public void Strength_ManInASlotOffHisLine_LiftsTheSlotsAxisAndNotHisOwn()
        {
            // The bug, stated as a measurement. Eleven identical players, all centre-backs, in a shape that
            // asks for three forwards: if each man is filed under his own line, the attack axis comes from
            // the empty-line fallback (the whole-lineup average) and a back eleven attacks like a real team.
            var builder = new EffectiveStrengthBuilder();
            IReadOnlyList<SlottedPlayer> allDefenders = SheetOf(Formation.F433, PlayerRole.CentreBack);

            TeamStrength strength = builder.Build(allDefenders, Tactics.Balanced);

            Assert.That(strength.Attack, Is.LessThan(strength.Defence),
                "Eleven centre-backs must be worse going forward than at the back.");
        }

        [Test]
        public void Strength_NaturalEleven_BeatsTheSameElevenScrambled()
        {
            // Same eleven players, same formation, one of them in the wrong slot — and the difference must
            // show up in the team the match is played with, or the team sheet is decoration.
            var builder = new EffectiveStrengthBuilder();
            List<SlottedPlayer> natural = NaturalSheet(Formation.F442);
            List<SlottedPlayer> swapped = NaturalSheet(Formation.F442);

            // Swap the men in the right-back and striker slots: both stay on the sheet, both are now out of
            // position, and nobody has been made better or worse as a footballer.
            SlottedPlayer back = swapped[1];
            SlottedPlayer forward = swapped[9];
            swapped[1] = new SlottedPlayer(back.Slot, back.Role, forward.Player);
            swapped[9] = new SlottedPlayer(forward.Slot, forward.Role, back.Player);

            TeamStrength before = builder.Build(natural, Tactics.Balanced);
            TeamStrength after = builder.Build(swapped, Tactics.Balanced);

            Assert.That(after.Attack, Is.LessThan(before.Attack));
            Assert.That(after.Defence, Is.LessThan(before.Defence));
        }

        [Test]
        public void Strength_ElevenPlayersWithNoSheet_IsUnchargedAndUnchanged()
        {
            // The other overload's meaning, pinned: a bare list of players declares no shape, so every man is
            // in his own role, nobody is out of position, and the number is exactly what it was before any of
            // this existed. Whole-squad strength (a club's rating in the table) depends on it.
            var builder = new EffectiveStrengthBuilder();
            List<SlottedPlayer> sheet = NaturalSheet(Formation.F442);
            var players = new List<Player>(sheet.Count);
            for (int i = 0; i < sheet.Count; i++)
            {
                players.Add(sheet[i].Player);
            }

            TeamStrength fromSheet = builder.Build(sheet, Tactics.Balanced);
            TeamStrength fromList = builder.Build(players, Tactics.Balanced);

            Assert.That(fromList.Attack, Is.EqualTo(fromSheet.Attack).Within(1e-9));
            Assert.That(fromList.Midfield, Is.EqualTo(fromSheet.Midfield).Within(1e-9));
            Assert.That(fromList.Defence, Is.EqualTo(fromSheet.Defence).Within(1e-9));
        }

        [Test]
        public void AutoPick_AcrossManySquadsAndShapes_FieldsMostlyNaturals()
        {
            // The believability guard on the whole arrangement, because the two halves fail in opposite
            // directions and both look fine in isolation: too small a penalty and the auto-pick becomes
            // "field the eleven best names anywhere", too large and no manager would ever move anybody.
            // Measured over 200 generated squads in all five shapes: 82% natural, 15% a better player one
            // role along, 2% an emergency, none absurd — which is what a real team sheet looks like.
            var selector = new LineupSelector();
            int natural = 0;
            int total = 0;
            for (int club = 0; club < 200; club++)
            {
                Squad squad = new SquadGenerator(new PlayerGenerator())
                    .Generate(club * 100, new GenerationContext(), new SplitMix64RandomNumberGenerator((ulong)(club + 1)));
                foreach (Formation formation in Formation.Presets)
                {
                    IReadOnlyList<SlottedPlayer> sheet = selector.SelectBest(squad, formation);
                    for (int i = 0; i < sheet.Count; i++)
                    {
                        PositionalFit fit = sheet[i].Fit;
                        Assert.That(fit, Is.Not.EqualTo(PositionalFit.Impossible),
                            "The auto-pick volunteered a keeper out of goal, or an outfielder in it.");
                        Assert.That(fit, Is.Not.EqualTo(PositionalFit.DistantLine),
                            "The auto-pick sent somebody two lines out of position with a squad to choose from.");
                        if (fit == PositionalFit.Natural)
                        {
                            natural++;
                        }

                        total++;
                    }
                }
            }

            double share = (double)natural / total;
            Assert.That(share, Is.GreaterThan(0.7), $"Only {share:P0} of slots went to a natural — the penalty is too small to matter.");
            Assert.That(share, Is.LessThan(0.95), $"{share:P0} of slots went to a natural — the penalty is so large that ability stopped counting.");
        }

        // An eleven in which every man is a natural for his slot, all equally good, so any difference a test
        // measures afterwards is the fit and nothing else.
        private static List<SlottedPlayer> NaturalSheet(Formation formation)
        {
            IReadOnlyList<PlayerRole> slots = formation.Slots;
            var sheet = new List<SlottedPlayer>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                sheet.Add(new SlottedPlayer(i, slots[i], Outfielder(i + 1, slots[i], 70)));
            }

            return sheet;
        }

        private static IReadOnlyList<SlottedPlayer> SheetOf(Formation formation, PlayerRole everyonesRole)
        {
            IReadOnlyList<PlayerRole> slots = formation.Slots;
            var sheet = new List<SlottedPlayer>(slots.Count);
            for (int i = 0; i < slots.Count; i++)
            {
                sheet.Add(new SlottedPlayer(i, slots[i], Outfielder(i + 1, everyonesRole, 70)));
            }

            return sheet;
        }

        private static Player Outfielder(int id, PlayerRole role, byte rating)
        {
            return new Player(new PlayerId(id), "P" + id, "England", role, 25, Uniform(rating), 75);
        }

        // Flat attributes, so a role rating is exactly the number given: the weights sum to 1 per role, so a
        // uniform sheet collapses to that value whatever the role, and every comparison here is arithmetic.
        private static Attributes Uniform(byte stat)
        {
            return new Attributes
            {
                Finishing = stat,
                Technique = stat,
                FirstTouch = stat,
                Dribbling = stat,
                Passing = stat,
                Crossing = stat,
                Heading = stat,
                LongShots = stat,
                Marking = stat,
                Tackling = stat,
                Penalties = stat,
                FreeKicks = stat,
                Corners = stat,
                LongThrows = stat,
                Pace = stat,
                Acceleration = stat,
                Stamina = stat,
                Strength = stat,
                Agility = stat,
                Jumping = stat,
                Balance = stat,
                Positioning = stat,
                Reflexes = stat,
                Handling = stat,
                AerialReach = stat,
                CommandOfArea = stat,
                OneOnOnes = stat,
                Kicking = stat,
                GkPositioning = stat,
            };
        }
    }
}
