using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    public sealed class AttributesTests
    {
        // DELETED: Initializer_EachGroup_RoundTrips and UnsetFields_DefaultToZero. The first assigned
        // eight auto-properties through an object initializer and read the same eight back; the second
        // asserted that an unassigned byte is 0. Neither can fail while the file compiles — they tested
        // the C# compiler, not Attributes. Every member's read path is covered for real by
        // ValueOf_EachAttribute_ReadsItsOwnProperty (on a sheet where no two values are alike) and its
        // write path by WithValue_WritesOneAttributeAndLeavesTheRestAlone (which starts from `default`,
        // so it also pins the zero-initialisation the deleted test claimed).

        [Test]
        public void Equality_SameValues_AreEqual()
        {
            Attributes left = Sample();
            Attributes right = Sample();

            Assert.That(left, Is.EqualTo(right));
            Assert.That(left == right, Is.True);
            Assert.That(left.GetHashCode(), Is.EqualTo(right.GetHashCode()));
        }

        [Test]
        public void Equality_DifferentStat_AreNotEqual()
        {
            Attributes baseline = Sample();
            Attributes faster = Sample();
            faster.Pace = 99;

            Assert.That(baseline, Is.Not.EqualTo(faster));
            Assert.That(baseline != faster, Is.True);
        }

        [Test]
        public void Equality_DifferentGoalkeepingStat_AreNotEqual()
        {
            Attributes baseline = Sample();
            Attributes sharper = Sample();
            sharper.Reflexes = 88;

            Assert.That(baseline, Is.Not.EqualTo(sharper));
        }

        [Test]
        public void ValueOf_EachAttribute_ReadsItsOwnProperty()
        {
            // ValueOf is the generic reader the shared role table depends on: PlayerRatings, PlayerDevelopment
            // and the scout all reach a stat through a PlayerAttribute rather than by naming a property. A
            // single mis-typed case here would misprice a role for good, so every member is checked against
            // the property it stands for, on a sheet where no two values are alike.
            Attributes sheet = Distinct();

            Assert.That(sheet.ValueOf(PlayerAttribute.Finishing), Is.EqualTo(sheet.Finishing));
            Assert.That(sheet.ValueOf(PlayerAttribute.Technique), Is.EqualTo(sheet.Technique));
            Assert.That(sheet.ValueOf(PlayerAttribute.FirstTouch), Is.EqualTo(sheet.FirstTouch));
            Assert.That(sheet.ValueOf(PlayerAttribute.Dribbling), Is.EqualTo(sheet.Dribbling));
            Assert.That(sheet.ValueOf(PlayerAttribute.Passing), Is.EqualTo(sheet.Passing));
            Assert.That(sheet.ValueOf(PlayerAttribute.Crossing), Is.EqualTo(sheet.Crossing));
            Assert.That(sheet.ValueOf(PlayerAttribute.Heading), Is.EqualTo(sheet.Heading));
            Assert.That(sheet.ValueOf(PlayerAttribute.LongShots), Is.EqualTo(sheet.LongShots));
            Assert.That(sheet.ValueOf(PlayerAttribute.Marking), Is.EqualTo(sheet.Marking));
            Assert.That(sheet.ValueOf(PlayerAttribute.Tackling), Is.EqualTo(sheet.Tackling));
            Assert.That(sheet.ValueOf(PlayerAttribute.Penalties), Is.EqualTo(sheet.Penalties));
            Assert.That(sheet.ValueOf(PlayerAttribute.FreeKicks), Is.EqualTo(sheet.FreeKicks));
            Assert.That(sheet.ValueOf(PlayerAttribute.Corners), Is.EqualTo(sheet.Corners));
            Assert.That(sheet.ValueOf(PlayerAttribute.LongThrows), Is.EqualTo(sheet.LongThrows));
            Assert.That(sheet.ValueOf(PlayerAttribute.Pace), Is.EqualTo(sheet.Pace));
            Assert.That(sheet.ValueOf(PlayerAttribute.Acceleration), Is.EqualTo(sheet.Acceleration));
            Assert.That(sheet.ValueOf(PlayerAttribute.Stamina), Is.EqualTo(sheet.Stamina));
            Assert.That(sheet.ValueOf(PlayerAttribute.Strength), Is.EqualTo(sheet.Strength));
            Assert.That(sheet.ValueOf(PlayerAttribute.Agility), Is.EqualTo(sheet.Agility));
            Assert.That(sheet.ValueOf(PlayerAttribute.Jumping), Is.EqualTo(sheet.Jumping));
            Assert.That(sheet.ValueOf(PlayerAttribute.Balance), Is.EqualTo(sheet.Balance));
            Assert.That(sheet.ValueOf(PlayerAttribute.Positioning), Is.EqualTo(sheet.Positioning));
            Assert.That(sheet.ValueOf(PlayerAttribute.Reflexes), Is.EqualTo(sheet.Reflexes));
            Assert.That(sheet.ValueOf(PlayerAttribute.Handling), Is.EqualTo(sheet.Handling));
            Assert.That(sheet.ValueOf(PlayerAttribute.AerialReach), Is.EqualTo(sheet.AerialReach));
            Assert.That(sheet.ValueOf(PlayerAttribute.CommandOfArea), Is.EqualTo(sheet.CommandOfArea));
            Assert.That(sheet.ValueOf(PlayerAttribute.OneOnOnes), Is.EqualTo(sheet.OneOnOnes));
            Assert.That(sheet.ValueOf(PlayerAttribute.Kicking), Is.EqualTo(sheet.Kicking));
            Assert.That(sheet.ValueOf(PlayerAttribute.GkPositioning), Is.EqualTo(sheet.GkPositioning));
        }

        [Test]
        public void WithValue_WritesOneAttributeAndLeavesTheRestAlone()
        {
            // The write side of the same mapping: development moves a role's attributes through it, so each
            // member must land in its own slot and touch nothing else (an empty sheet makes a stray write
            // visible as a non-zero stat).
            var all = (PlayerAttribute[])System.Enum.GetValues(typeof(PlayerAttribute));

            Attributes empty = default;
            foreach (PlayerAttribute attribute in all)
            {
                Attributes written = empty.WithValue(attribute, 77);
                foreach (PlayerAttribute other in all)
                {
                    byte expected = other == attribute ? (byte)77 : (byte)0;
                    Assert.That(written.ValueOf(other), Is.EqualTo(expected), $"writing {attribute} affected {other}");
                }
            }
        }

        [Test]
        public void WithValue_DoesNotMutateTheSourceSheet()
        {
            Attributes before = Sample();

            Attributes after = before.WithValue(PlayerAttribute.Pace, 99);

            Assert.That(after.Pace, Is.EqualTo(99));
            Assert.That(before.Pace, Is.EqualTo(77), "a copy is returned; the sheet handed in is unchanged");
        }

        // A sheet where every attribute holds a different number, so a reader that returns the wrong one
        // cannot pass by coincidence.
        private static Attributes Distinct()
        {
            return new Attributes
            {
                Finishing = 1,
                Technique = 2,
                FirstTouch = 3,
                Dribbling = 4,
                Passing = 5,
                Crossing = 6,
                Heading = 7,
                LongShots = 8,
                Marking = 9,
                Tackling = 10,
                Penalties = 11,
                FreeKicks = 12,
                Corners = 13,
                LongThrows = 14,
                Pace = 15,
                Acceleration = 16,
                Stamina = 17,
                Strength = 18,
                Agility = 19,
                Jumping = 20,
                Balance = 21,
                Positioning = 22,
                Reflexes = 23,
                Handling = 24,
                AerialReach = 25,
                CommandOfArea = 26,
                OneOnOnes = 27,
                Kicking = 28,
                GkPositioning = 29,
            };
        }

        private static Attributes Sample()
        {
            return new Attributes
            {
                Finishing = 70,
                Technique = 65,
                Passing = 72,
                Tackling = 40,
                Marking = 44,
                Positioning = 68,
                Pace = 77,
                Stamina = 80,
                Strength = 61,
                Reflexes = 30,
            };
        }
    }
}
