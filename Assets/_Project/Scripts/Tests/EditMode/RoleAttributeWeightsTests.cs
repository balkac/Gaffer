using System;
using System.Collections.Generic;
using Gaffer.Application.Progression;
using Gaffer.Application.Simulation;
using Gaffer.Common;
using Gaffer.Domain.Players;
using NUnit.Framework;

namespace Gaffer.Tests
{
    /// <summary>
    /// The guard ARCHITECTURE §8a implies for a table with a single owner: everything derived from
    /// <see cref="RoleAttributeWeights"/> must still agree with it. "The rating weights these, development
    /// grows those, the scout shows the others" used to be kept in step by a comment, and it had already
    /// drifted — so the relationship is asserted here instead of described.
    /// </summary>
    public sealed class RoleAttributeWeightsTests
    {
        private static readonly PlayerRole[] AllRoles = (PlayerRole[])Enum.GetValues(typeof(PlayerRole));

        private static readonly PlayerAttribute[] AllAttributes =
            (PlayerAttribute[])Enum.GetValues(typeof(PlayerAttribute));

        private static Attributes Uniform(byte stat)
        {
            Attributes attributes = default;
            for (int i = 0; i < AllAttributes.Length; i++)
            {
                attributes = attributes.WithValue(AllAttributes[i], stat);
            }

            return attributes;
        }

        private static Player Player(PlayerRole role, int age, byte ability, byte potential)
        {
            return new Player(new PlayerId(1), "Test", "England", role, age, Uniform(ability), potential);
        }

        private static IRandom Rng(ulong seed)
        {
            return new SplitMix64RandomNumberGenerator(seed);
        }

        private static bool IsRated(PlayerRole role, PlayerAttribute attribute)
        {
            IReadOnlyList<RoleAttributeWeight> weights = RoleAttributeWeights.For(role);
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i].Attribute == attribute)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsAthletic(PlayerAttribute attribute)
        {
            IReadOnlyList<PlayerAttribute> athletic = PlayerAttributes.Athletic;
            for (int i = 0; i < athletic.Count; i++)
            {
                if (athletic[i] == attribute)
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void For_EveryRole_WeightsSumToOne()
        {
            // The normalization is what keeps a rating on the 0-100 scale and what lets development raise
            // every rated attribute by one amount to move the rating by that amount. A new role that misses
            // it would quietly rescale valuations and wages, so it fails here instead.
            foreach (PlayerRole role in AllRoles)
            {
                IReadOnlyList<RoleAttributeWeight> weights = RoleAttributeWeights.For(role);
                double total = 0.0;
                for (int i = 0; i < weights.Count; i++)
                {
                    total += weights[i].Weight;
                }

                Assert.That(total, Is.EqualTo(1.0).Within(1e-9), $"{role} weights sum to {total}, not 1.");
            }
        }

        [Test]
        public void For_EveryRole_NamesEachAttributeOnce()
        {
            foreach (PlayerRole role in AllRoles)
            {
                IReadOnlyList<RoleAttributeWeight> weights = RoleAttributeWeights.For(role);
                var seen = new HashSet<PlayerAttribute>();
                Assert.That(weights.Count, Is.GreaterThan(0), $"{role} has no attributes.");
                for (int i = 0; i < weights.Count; i++)
                {
                    Assert.That(seen.Add(weights[i].Attribute), Is.True,
                        $"{role} lists {weights[i].Attribute} twice, so its weight is applied twice.");
                    Assert.That(weights[i].Weight, Is.GreaterThan(0.0), $"{role} weights {weights[i].Attribute} at zero.");
                }
            }
        }

        [Test]
        public void ForRole_UniformSheet_CollapsesToThatValueForEveryRole()
        {
            // The arithmetic consequence of the weights summing to 1, asserted for all twelve roles rather
            // than the four PlayerRatingsTests samples — this is what pins the table's numbers.
            Attributes uniform = Uniform(60);

            foreach (PlayerRole role in AllRoles)
            {
                Assert.That(PlayerRatings.ForRole(role, uniform), Is.EqualTo(60.0).Within(1e-9), role.ToString());
            }
        }

        [Test]
        public void Develop_YoungPlayer_GrowsExactlyTheRatedAttributes()
        {
            // The invariant the old three-table arrangement could break silently: growth must lift the
            // attributes the rating reads, all of them, and nothing else. A teenager far below his ceiling
            // grows by several whole points on every rated attribute whatever the season variance rolls, so
            // "changed" is unambiguous.
            var dev = new PlayerDevelopment();

            foreach (PlayerRole role in AllRoles)
            {
                Player before = Player(role, 18, 50, 88);
                Player after = dev.Develop(before, Rng(7));

                foreach (PlayerAttribute attribute in AllAttributes)
                {
                    byte start = before.Attributes.ValueOf(attribute);
                    byte end = after.Attributes.ValueOf(attribute);
                    if (IsRated(role, attribute))
                    {
                        Assert.That(end, Is.GreaterThan(start), $"{role}: {attribute} is rated but did not grow.");
                    }
                    else
                    {
                        Assert.That(end, Is.EqualTo(start), $"{role}: {attribute} grew but no role rating reads it.");
                    }
                }
            }
        }

        [Test]
        public void Develop_Veteran_ErodesOnlyTheRatedAndAthleticAttributes()
        {
            // The documented exception set: age erodes a veteran's rated attributes (his quality slips) and
            // the athletic ones (his body goes), and nothing else. Acceleration, agility and jumping are in
            // the second group without being in the first for any role — see PlayerAttributes.Athletic —
            // which is a real asymmetry, so it is pinned rather than tidied away.
            var dev = new PlayerDevelopment(new DevelopmentSettings { DeclinePerYear = 3.0 });

            foreach (PlayerRole role in AllRoles)
            {
                Player before = Player(role, 40, 78, 60);
                Player after = dev.Develop(before, Rng(9));

                foreach (PlayerAttribute attribute in AllAttributes)
                {
                    byte start = before.Attributes.ValueOf(attribute);
                    byte end = after.Attributes.ValueOf(attribute);
                    if (IsRated(role, attribute) || IsAthletic(attribute))
                    {
                        Assert.That(end, Is.LessThan(start), $"{role}: {attribute} should have worn down.");
                    }
                    else
                    {
                        Assert.That(end, Is.EqualTo(start), $"{role}: age moved {attribute}, which nothing reads.");
                    }
                }
            }
        }

        [Test]
        public void Athletic_EveryAttributeAgeErodes_IsRatedBySomeRole()
        {
            // The invariant the 2026-08-07 fix established, and the reason this test exists rather than the
            // one it replaces. Ageing erodes the athletic attributes on top of the general slide, so if an
            // eroded attribute is weighted by NO role, age is quietly lowering a number nothing reads and
            // the veteran's decline never reaches his OVR. Acceleration, agility and jumping sat in exactly
            // that hole until unifying the role tables made it visible.
            //
            // Coverage across all roles is the invariant — NOT that every role rates every athletic
            // attribute. A holding midfielder's rating carries none of the three on merit, and that is
            // correct: the point is that nothing age takes away is invisible everywhere.
            var unrated = new List<PlayerAttribute>();
            foreach (PlayerAttribute attribute in PlayerAttributes.Athletic)
            {
                bool ratedSomewhere = false;
                foreach (PlayerRole role in AllRoles)
                {
                    ratedSomewhere |= IsRated(role, attribute);
                }

                if (!ratedSomewhere)
                {
                    unrated.Add(attribute);
                }
            }

            Assert.That(unrated, Is.Empty,
                "Age erodes these attributes but no role's rating reads them, so the decline is invisible in "
                + "OVR. Either weight them in RoleAttributeWeights or take them out of PlayerAttributes.Athletic.");
        }

        [Test]
        public void RoleKeyAttributes_ForEveryRole_ProjectTheWeightTableInOrder()
        {
            // The scout's rows are the role's rating attributes, in the same order — not a parallel list a
            // reader has to trust a comment about.
            foreach (PlayerRole role in AllRoles)
            {
                IReadOnlyList<RoleAttributeWeight> weights = RoleAttributeWeights.For(role);
                IReadOnlyList<AttributeKey> keys = RoleKeyAttributes.For(role);

                Assert.That(keys.Count, Is.EqualTo(weights.Count), $"{role} shows a different number of rows than it is scored on.");
                for (int i = 0; i < weights.Count; i++)
                {
                    Assert.That(keys[i].Attribute, Is.EqualTo(weights[i].Attribute), $"{role} row {i}");
                }
            }
        }

        [Test]
        public void RoleKeyAttributes_ForALine_MatchesItsRepresentativeRole()
        {
            foreach (Position line in (Position[])Enum.GetValues(typeof(Position)))
            {
                IReadOnlyList<AttributeKey> byLine = RoleKeyAttributes.For(line);
                IReadOnlyList<AttributeKey> byRole = RoleKeyAttributes.For(PlayerRoles.Representative(line));

                Assert.That(byLine.Count, Is.EqualTo(byRole.Count), line.ToString());
                for (int i = 0; i < byLine.Count; i++)
                {
                    Assert.That(byLine[i].Attribute, Is.EqualTo(byRole[i].Attribute), $"{line} row {i}");
                }
            }
        }

        [Test]
        public void AttributeKey_ReadsTheAttributeItNames()
        {
            var sheet = new Attributes { Finishing = 71, Pace = 44 };

            Assert.That(new AttributeKey(PlayerAttribute.Finishing).Read(sheet), Is.EqualTo(71));
            Assert.That(new AttributeKey(PlayerAttribute.Pace).Read(sheet), Is.EqualTo(44));
        }

        [Test]
        public void GetLabelKey_EveryAttribute_IsAUniqueLocalizationKey()
        {
            // NON-NEGOTIABLE #8: the Domain hands the UI a key, never the words.
            var seen = new HashSet<string>();
            foreach (PlayerAttribute attribute in AllAttributes)
            {
                string key = PlayerAttributes.GetLabelKey(attribute);
                Assert.That(key, Does.StartWith("attr."), attribute.ToString());
                Assert.That(seen.Add(key), Is.True, $"Localization key '{key}' is used by more than one attribute.");
            }
        }
    }
}
