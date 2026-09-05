using System.Collections.Generic;
using Gaffer.Application.Generation;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Clubs;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class LineupSelectorTests
    {
        private static Squad GeneratedSquad()
        {
            return GeneratedSquad(idBase: 0, seed: 7);
        }

        private static Squad GeneratedSquad(int idBase, ulong seed)
        {
            return new SquadGenerator(new PlayerGenerator())
                .Generate(idBase, new GenerationContext(), new SplitMix64RandomNumberGenerator(seed));
        }

        private static int[] IdsOf(IReadOnlyList<SlottedPlayer> sheet)
        {
            var ids = new int[sheet.Count];
            for (int i = 0; i < sheet.Count; i++)
            {
                ids[i] = sheet[i].Player.Id.Value;
            }

            return ids;
        }

        [Test]
        public void SelectBest_FillsEverySlotOnce()
        {
            IReadOnlyList<SlottedPlayer> sheet = new LineupSelector().SelectBest(GeneratedSquad(), Formation.F433);

            Assert.That(sheet.Count, Is.EqualTo(11));
            var seen = new HashSet<int>();
            for (int i = 0; i < sheet.Count; i++)
            {
                Assert.That(sheet[i].Slot, Is.EqualTo(i), "Picks come back in slot order, and every pick names its slot.");
                Assert.That(sheet[i].Role, Is.EqualTo(Formation.F433.Slots[i]));
                Assert.That(seen.Add(sheet[i].Player.Id.Value), Is.True, "The same player was picked twice.");
            }
        }

        [Test]
        public void SelectBest_NeverPutsAnOutfielderInGoal()
        {
            // The penalty alone does not stop this: a good striker at 60% still out-rates a poor keeper, so
            // ranking on rating with no ban would auto-pick him between the posts. The auto-pick refuses the
            // pairing outright; a manager who wants it must ask for it.
            Squad squad = GeneratedSquad();
            IReadOnlyList<SlottedPlayer> sheet = new LineupSelector().SelectBest(squad, Formation.F442);

            Assert.That(sheet[0].Role, Is.EqualTo(PlayerRole.Goalkeeper));
            Assert.That(sheet[0].Player.Role, Is.EqualTo(PlayerRole.Goalkeeper));
            for (int i = 1; i < sheet.Count; i++)
            {
                Assert.That(sheet[i].Player.Role, Is.Not.EqualTo(PlayerRole.Goalkeeper),
                    "A keeper was picked in an outfield slot.");
            }
        }

        [Test]
        public void SelectBest_MuchBetterPlayerOnTheSameLine_TakesTheSlotFromItsNatural()
        {
            // The rule this replaced tried an exact role first and never looked past it, so a 40-rated
            // natural right-midfielder always beat a 95-rated central midfielder. Being on the wrong side of
            // the same line costs about a tenth of a player, and a tenth of 95 is not 55.
            Squad squad = SquadOf(
                Outfielder(1, PlayerRole.RightMidfield, 40),
                Outfielder(2, PlayerRole.CentralMidfield, 95));

            SlottedPlayer pick = PickAt(squad, PlayerRole.RightMidfield);

            Assert.That(pick.Player.Id.Value, Is.EqualTo(2));
            Assert.That(pick.Fit, Is.EqualTo(PositionalFit.SameLine));
        }

        [Test]
        public void SelectBest_MarginallyBetterPlayerOffTheLine_LosesToItsNatural()
        {
            // The other half of the same rule, and the half that makes it a decision rather than a
            // free-for-all: 72 charged for a line out of place is worse than 70 in the right one.
            Squad squad = SquadOf(
                Outfielder(1, PlayerRole.CentralMidfield, 70),
                Outfielder(2, PlayerRole.CentreBack, 72));

            SlottedPlayer pick = PickAt(squad, PlayerRole.CentralMidfield);

            Assert.That(pick.Player.Id.Value, Is.EqualTo(1));
            Assert.That(pick.Fit, Is.EqualTo(PositionalFit.Natural));
        }

        // The first slot of a one-slot formation, so a preference can be read off a two-player squad without
        // the rest of an eleven getting in the way.
        private static SlottedPlayer PickAt(Squad squad, PlayerRole slot)
        {
            var formation = new Formation("test", new[] { slot });
            IReadOnlyList<SlottedPlayer> sheet = new LineupSelector().SelectBest(squad, formation);
            Assert.That(sheet.Count, Is.EqualTo(1));
            return sheet[0];
        }

        private static Squad SquadOf(params Player[] players)
        {
            return new Squad(players);
        }

        // Flat attributes, so the role rating is exactly the number given (the weights sum to 1 per role)
        // and a preference is arithmetic rather than a guess about which attribute a role reads.
        private static Player Outfielder(int id, PlayerRole role, byte rating)
        {
            return new Player(new PlayerId(id), "P" + id, "England", role, 25, Uniform(rating), 75);
        }

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

        [Test]
        public void SelectBest_OneInstanceReusedAcrossSquads_LeavesNoResidue()
        {
            // SelectBest takes no IRandom and is a pure function of (squad, formation), so two separate
            // selectors agreeing proves nothing. The real risk is the scratch state one instance carries
            // between calls — the not-yet-picked pool and the returned eleven are both reused buffers
            // (PERFORMANCE §4, reuse scratch collections). So: ONE instance, squad A → squad B → squad A,
            // and run 3 must reproduce run 1 exactly. The ids are copied out of each result immediately
            // because SelectBest's contract is that its list is only valid until the next call — which
            // the intervening B pick is precisely what invalidates.
            var selector = new LineupSelector();
            Squad a = GeneratedSquad(idBase: 0, seed: 7);
            Squad b = GeneratedSquad(idBase: 500, seed: 31);

            int[] first = IdsOf(selector.SelectBest(a, Formation.F352));
            int[] between = IdsOf(selector.SelectBest(b, Formation.F433));
            int[] third = IdsOf(selector.SelectBest(a, Formation.F352));

            Assert.That(first.Length, Is.EqualTo(11));
            Assert.That(between.Length, Is.EqualTo(11));
            Assert.That(third, Is.EqualTo(first), "The A pick changed after an intervening B pick — the reused buffers leaked state.");
        }

        [Test]
        public void SelectBest_SameSquadTwiceOnOneInstance_IsIdentical()
        {
            var selector = new LineupSelector();
            Squad squad = GeneratedSquad();

            int[] first = IdsOf(selector.SelectBest(squad, Formation.F352));
            int[] second = IdsOf(selector.SelectBest(squad, Formation.F352));

            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SelectBest_StrongerEleven_OutratesTheWholeSquadOnItsAxis()
        {
            Squad squad = GeneratedSquad();
            var builder = new EffectiveStrengthBuilder();

            TeamStrength wholeSquad = builder.Build(squad);
            TeamStrength bestEleven = builder.Build(new LineupSelector().SelectBest(squad, Formation.F442), Tactics.Balanced);

            // Fielding the best eleven should be at least as strong up front as averaging the entire squad.
            Assert.That(bestEleven.Attack, Is.GreaterThanOrEqualTo(wholeSquad.Attack));
        }
    }
}
